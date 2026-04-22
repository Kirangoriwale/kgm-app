using KgmApp.Data;
using KgmApp.Models;
using KgmApp.Models.Reports;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Services;

/// <summary>
/// Computes total paid vs pending (contribution + registration) for one member — same rules as Contribution Report.
/// </summary>
public sealed class MemberContributionSummaryService
{
    private readonly AppDbContext _db;

    /// <summary>Must match <see cref="Controllers.ReportController"/> contribution window.</summary>
    private static readonly DateTime ContributionStartDate = new(2025, 4, 1);

    public MemberContributionSummaryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(decimal TotalPaid, decimal TotalPending)> GetPaidAndPendingAsync(int memberId,
        CancellationToken cancellationToken = default)
    {
        var core = await LoadCoreAsync(memberId, cancellationToken);
        if (core == null)
            return (0m, 0m);
        return (core.TotalPaid, core.TotalPending);
    }

    public async Task<MemberPaidDetailsViewModel?> GetPaidDetailsAsync(int memberId, string memberName,
        CancellationToken cancellationToken = default)
    {
        var core = await LoadCoreAsync(memberId, cancellationToken);
        if (core == null)
            return null;

        var contributionLines = core.ContributionTransactions
            .Select(t => new MemberContributionPaidLine
            {
                TransactionDate = t.TransactionDate,
                Amount = t.Amount,
                PaymentMode = t.PaymentMode,
                Description = string.IsNullOrWhiteSpace(t.Description) ? "—" : t.Description
            })
            .ToList();

        var registrationLines = new List<MemberRegistrationPaidLine>();
        foreach (var t in core.RegistrationTransactions.OrderBy(x => x.TransactionDate))
        {
            var label = t.Category == Transaction.CategoryMemberRegistration
                ? "Member (you)"
                : core.SubNameById.TryGetValue(t.SubMemberId ?? 0, out var nm)
                    ? $"SubMember: {nm}"
                    : "SubMember";
            registrationLines.Add(new MemberRegistrationPaidLine
            {
                PartyLabel = label,
                TransactionDate = t.TransactionDate,
                Amount = t.Amount
            });
        }

        return new MemberPaidDetailsViewModel
        {
            MemberName = memberName,
            ContributionPayments = contributionLines,
            ContributionsPaidTotal = core.PaidContributionsTotal,
            RegistrationPayments = registrationLines,
            RegistrationPaidTotal = core.RegistrationFeePaid,
            GrandTotal = core.TotalPaid
        };
    }

    public async Task<MemberPendingDetailsViewModel?> GetPendingDetailsAsync(int memberId, string memberName,
        CancellationToken cancellationToken = default)
    {
        var core = await LoadCoreAsync(memberId, cancellationToken);
        if (core == null)
            return null;

        var byPeriod = new List<MemberContributionPendingPeriodLine>();
        foreach (var p in core.ExpectedPeriods)
        {
            var expected = GetContributionFeeForDate(p.StartUtc, core.FeeSettings);
            var paidInPeriod = core.ContributionTransactions
                .Where(t => t.TransactionDate >= p.StartUtc && t.TransactionDate < p.EndUtcExclusive)
                .Sum(t => t.Amount);
            byPeriod.Add(new MemberContributionPendingPeriodLine
            {
                PeriodLabel = $"{p.FinancialYearLabel} · {p.PeriodLabel}",
                ExpectedFee = expected,
                PaidInPeriod = paidInPeriod
            });
        }

        return new MemberPendingDetailsViewModel
        {
            MemberName = memberName,
            ExpectedContributionTotal = core.ExpectedContributionTotal,
            PaidContributionTotal = core.PaidContributionsTotal,
            PendingContribution = Math.Max(0m, core.ExpectedContributionTotal - core.PaidContributionsTotal),
            ContributionByPeriod = byPeriod,
            RegistrationFeePerPerson = core.RegistrationFeePerPerson,
            SubMemberCount = core.SubMemberCount,
            ExpectedRegistrationTotal = core.ExpectedRegistrationTotal,
            PaidMemberRegistration = core.MemberRegAmount,
            PaidSubMemberRegistration = core.SubRegAmount,
            RegistrationPaidTotal = core.RegistrationFeePaid,
            PendingRegistration = Math.Max(0m, core.ExpectedRegistrationTotal - core.RegistrationFeePaid),
            GrandPendingTotal = core.TotalPending
        };
    }

    private async Task<ContributionCore?> LoadCoreAsync(int memberId, CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
        var todayUtcExclusive = DateTime.SpecifyKind(DateTime.Today.AddDays(1), DateTimeKind.Utc);

        var member = await _db.Members
            .AsNoTracking()
            .Include(m => m.SubMembers)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);

        if (member == null)
            return null;

        var subMemberIds = member.SubMembers.Select(s => s.Id).ToList();
        var subNameById = member.SubMembers.ToDictionary(s => s.Id, s => s.Name ?? string.Empty);

        var minTxDate = DateTime.SpecifyKind(ContributionStartDate.Date, DateTimeKind.Utc);

        var contributionRaw = await _db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.MemberId == memberId &&
                t.Category == Transaction.CategoryHalfYearlyContribution &&
                t.TransactionDate >= minTxDate &&
                t.TransactionDate < todayUtcExclusive)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .Select(t => new { t.TransactionDate, t.Amount, t.PaymentMode, t.Description })
            .ToListAsync(cancellationToken);

        var contributionTransactions = contributionRaw
            .Select(t => new ContributionTxRow(t.TransactionDate, t.Amount, t.PaymentMode, t.Description ?? string.Empty))
            .ToList();

        var registrationRaw = await _db.Transactions
            .AsNoTracking()
            .Where(t =>
                (t.MemberId == memberId && t.Category == Transaction.CategoryMemberRegistration) ||
                (t.SubMemberId.HasValue &&
                 subMemberIds.Contains(t.SubMemberId.Value) &&
                 t.Category == Transaction.CategorySubMemberRegistration))
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .Select(t => new { t.TransactionDate, t.Amount, t.Category, t.SubMemberId })
            .ToListAsync(cancellationToken);

        var registrationTransactions = registrationRaw
            .Select(t => new RegistrationTxRow(t.TransactionDate, t.Amount, t.Category, t.SubMemberId))
            .ToList();

        var memberRegAmount = registrationTransactions
            .Where(x => x.Category == Transaction.CategoryMemberRegistration)
            .Sum(x => x.Amount);

        var subIds = member.SubMembers.Select(s => s.Id).ToHashSet();
        var subRegAmount = registrationTransactions
            .Where(x => x.SubMemberId.HasValue && subIds.Contains(x.SubMemberId.Value))
            .Sum(x => x.Amount);

        var registrationFeePaid = memberRegAmount + subRegAmount;
        var paidContributionsTotal = contributionTransactions.Sum(x => x.Amount);

        var feeSettings = await _db.FeeSettings
            .AsNoTracking()
            .Where(x => x.ApplyFromDate <= todayUtcExclusive)
            .OrderBy(x => x.ApplyFromDate)
            .ToListAsync(cancellationToken);

        var periods = BuildHalfYearPeriods(ContributionStartDate, todayUtc);
        var expectedPeriods = periods.Where(p => p.StartUtc <= todayUtc).ToList();

        var expectedContributionTotal = expectedPeriods.Sum(p => GetContributionFeeForDate(p.StartUtc, feeSettings));
        var registrationFeePerPerson = GetRegistrationFeeForDate(todayUtc, feeSettings);
        var subCount = member.SubMembers.Count;
        var expectedRegistrationTotal = registrationFeePerPerson * (1 + subCount);
        var pendingContribution = expectedContributionTotal - paidContributionsTotal;
        var pendingRegistration = expectedRegistrationTotal - registrationFeePaid;
        var totalPending = Math.Max(0m, pendingContribution) + Math.Max(0m, pendingRegistration);
        var totalPaid = paidContributionsTotal + registrationFeePaid;

        return new ContributionCore(
            member,
            contributionTransactions,
            registrationTransactions,
            feeSettings,
            expectedPeriods,
            paidContributionsTotal,
            memberRegAmount,
            subRegAmount,
            registrationFeePaid,
            expectedContributionTotal,
            registrationFeePerPerson,
            subCount,
            expectedRegistrationTotal,
            totalPaid,
            totalPending,
            subNameById);
    }

    private sealed record ContributionTxRow(
        DateTime TransactionDate,
        decimal Amount,
        string PaymentMode,
        string Description);

    private sealed record RegistrationTxRow(
        DateTime TransactionDate,
        decimal Amount,
        string Category,
        int? SubMemberId);

    private sealed class ContributionCore
    {
        public ContributionCore(
            Member member,
            List<ContributionTxRow> contributionTransactions,
            List<RegistrationTxRow> registrationTransactions,
            List<FeeSetting> feeSettings,
            List<HalfYearPeriod> expectedPeriods,
            decimal paidContributionsTotal,
            decimal memberRegAmount,
            decimal subRegAmount,
            decimal registrationFeePaid,
            decimal expectedContributionTotal,
            decimal registrationFeePerPerson,
            int subMemberCount,
            decimal expectedRegistrationTotal,
            decimal totalPaid,
            decimal totalPending,
            Dictionary<int, string> subNameById)
        {
            Member = member;
            ContributionTransactions = contributionTransactions;
            RegistrationTransactions = registrationTransactions;
            FeeSettings = feeSettings;
            ExpectedPeriods = expectedPeriods;
            PaidContributionsTotal = paidContributionsTotal;
            MemberRegAmount = memberRegAmount;
            SubRegAmount = subRegAmount;
            RegistrationFeePaid = registrationFeePaid;
            ExpectedContributionTotal = expectedContributionTotal;
            RegistrationFeePerPerson = registrationFeePerPerson;
            SubMemberCount = subMemberCount;
            ExpectedRegistrationTotal = expectedRegistrationTotal;
            TotalPaid = totalPaid;
            TotalPending = totalPending;
            SubNameById = subNameById;
        }

        public Member Member { get; }
        public List<ContributionTxRow> ContributionTransactions { get; }
        public List<RegistrationTxRow> RegistrationTransactions { get; }
        public List<FeeSetting> FeeSettings { get; }
        public List<HalfYearPeriod> ExpectedPeriods { get; }
        public decimal PaidContributionsTotal { get; }
        public decimal MemberRegAmount { get; }
        public decimal SubRegAmount { get; }
        public decimal RegistrationFeePaid { get; }
        public decimal ExpectedContributionTotal { get; }
        public decimal RegistrationFeePerPerson { get; }
        public int SubMemberCount { get; }
        public decimal ExpectedRegistrationTotal { get; }
        public decimal TotalPaid { get; }
        public decimal TotalPending { get; }
        public Dictionary<int, string> SubNameById { get; }
    }

    private static decimal GetContributionFeeForDate(DateTime dateUtc, IReadOnlyList<FeeSetting> feeSettings) =>
        GetEffectiveFee(dateUtc, feeSettings)?.ContributionFee ?? 200m;

    private static decimal GetRegistrationFeeForDate(DateTime dateUtc, IReadOnlyList<FeeSetting> feeSettings) =>
        GetEffectiveFee(dateUtc, feeSettings)?.RegistrationFee ?? 50m;

    private static FeeSetting? GetEffectiveFee(DateTime dateUtc, IReadOnlyList<FeeSetting> feeSettings)
    {
        for (var i = feeSettings.Count - 1; i >= 0; i--)
        {
            if (feeSettings[i].ApplyFromDate <= dateUtc)
                return feeSettings[i];
        }

        return null;
    }

    private static List<HalfYearPeriod> BuildHalfYearPeriods(DateTime startDateLocal, DateTime todayUtc)
    {
        var startYear = startDateLocal.Year;
        var currentFyStartYear = todayUtc.Month >= 4 ? todayUtc.Year : todayUtc.Year - 1;
        if (currentFyStartYear < startYear)
            currentFyStartYear = startYear;

        var periods = new List<HalfYearPeriod>();

        for (var fyStartYear = startYear; fyStartYear <= currentFyStartYear; fyStartYear++)
        {
            var label = $"{fyStartYear}-{(fyStartYear + 1).ToString().Substring(2)}";
            var apr1 = DateTime.SpecifyKind(new DateTime(fyStartYear, 4, 1), DateTimeKind.Utc);
            var oct1 = DateTime.SpecifyKind(new DateTime(fyStartYear, 10, 1), DateTimeKind.Utc);
            var nextApr1 = DateTime.SpecifyKind(new DateTime(fyStartYear + 1, 4, 1), DateTimeKind.Utc);

            periods.Add(new HalfYearPeriod
            {
                FinancialYearLabel = label,
                PeriodLabel = "Apr–Sep",
                StartUtc = apr1,
                EndUtcExclusive = oct1
            });

            periods.Add(new HalfYearPeriod
            {
                FinancialYearLabel = label,
                PeriodLabel = "Oct–Mar",
                StartUtc = oct1,
                EndUtcExclusive = nextApr1
            });
        }

        var startUtc = DateTime.SpecifyKind(startDateLocal.Date, DateTimeKind.Utc);
        return periods.Where(p => p.EndUtcExclusive > startUtc).ToList();
    }
}
