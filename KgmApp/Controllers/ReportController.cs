using KgmApp.Data;
using KgmApp.Models;
using KgmApp.Models.Meetings;
using KgmApp.Models.Reports;
using KgmApp.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public sealed class ReportController : Controller
{
    private readonly AppDbContext _db;

    public ReportController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> ContributionReport()
    {
        var startDate = new DateTime(2025, 4, 1);
        var todayUtc = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
        var todayUtcExclusive = DateTime.SpecifyKind(DateTime.Today.AddDays(1), DateTimeKind.Utc);

        var periods = BuildHalfYearPeriods(startDate, todayUtc);
        var expectedPeriods = periods.Where(p => p.StartUtc <= todayUtc).ToList();

        var members = await _db.Members
            .AsNoTracking()
            .Include(m => m.SubMembers)
            .OrderBy(m => m.Sr)
            .ThenBy(m => m.Name)
            .ToListAsync();

        var memberIds = members.Select(m => m.Id).ToList();

        var minTxDate = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var maxTxDateExclusive = todayUtcExclusive;

        var contributionTx = await _db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.MemberId.HasValue &&
                memberIds.Contains(t.MemberId.Value) &&
                t.Category == Transaction.CategoryHalfYearlyContribution &&
                t.TransactionDate >= minTxDate &&
                t.TransactionDate < maxTxDateExclusive)
            .Select(t => new { MemberId = t.MemberId!.Value, t.TransactionDate, t.Amount })
            .ToListAsync();

        var contributionLookup = contributionTx
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => (x.TransactionDate, x.Amount)).ToList()
            );

        var registrationTx = await _db.Transactions
            .AsNoTracking()
            .Where(t =>
                (t.MemberId.HasValue && memberIds.Contains(t.MemberId.Value) && t.Category == Transaction.CategoryMemberRegistration) ||
                (t.SubMemberId.HasValue && t.Category == Transaction.CategorySubMemberRegistration))
            .Select(t => new { t.MemberId, t.SubMemberId, t.Category, t.Amount })
            .ToListAsync();

        var memberRegLookup = registrationTx
            .Where(x => x.MemberId.HasValue && x.Category == Transaction.CategoryMemberRegistration)
            .GroupBy(x => x.MemberId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var subMemberRegLookup = registrationTx
            .Where(x => x.SubMemberId.HasValue && x.Category == Transaction.CategorySubMemberRegistration)
            .GroupBy(x => x.SubMemberId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var feeSettings = await _db.FeeSettings
            .AsNoTracking()
            .Where(x => x.ApplyFromDate <= todayUtcExclusive)
            .OrderBy(x => x.ApplyFromDate)
            .ToListAsync();

        var rows = new List<ContributionReportRowViewModel>(members.Count);

        foreach (var m in members)
        {
            var subs = (m.SubMembers ?? [])
                .OrderBy(s => s.SrNo)
                .ThenBy(s => s.Name)
                .ToList();

            var sub1 = subs.Count > 0 ? subs[0].Name : string.Empty;
            var sub2 = subs.Count > 1 ? subs[1].Name : string.Empty;

            var paidByPeriod = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var paidTotal = 0m;

            contributionLookup.TryGetValue(m.Id, out var dates);
            dates ??= [];

            foreach (var p in periods)
            {
                var amountInPeriod = dates
                    .Where(x => x.TransactionDate >= p.StartUtc && x.TransactionDate < p.EndUtcExclusive)
                    .Sum(x => x.Amount);

                paidByPeriod[p.Key] = amountInPeriod;
            }

            // Pending is based on total expected up to today minus total amount paid (even if arrears were paid later).
            paidTotal = dates
                .Where(x => x.TransactionDate >= minTxDate && x.TransactionDate < todayUtcExclusive)
                .Sum(x => x.Amount);

            var registrationFeePaid = memberRegLookup.TryGetValue(m.Id, out var memberRegAmount) ? memberRegAmount : 0m;

            foreach (var s in subs)
            {
                if (subMemberRegLookup.TryGetValue(s.Id, out var subRegAmount))
                    registrationFeePaid += subRegAmount;
            }

            var expectedTotal = expectedPeriods.Sum(p => GetContributionFeeForDate(p.StartUtc, feeSettings));
            var registrationFeeForToday = GetRegistrationFeeForDate(todayUtc, feeSettings);
            var expectedRegistrationFee = registrationFeeForToday * (1 + subs.Count);
            var expectedGrandTotal = expectedTotal + expectedRegistrationFee;
            var totalPaid = paidTotal + registrationFeePaid;
            var pendingContribution = expectedTotal - paidTotal;
            var pendingRegistration = expectedRegistrationFee - registrationFeePaid;
            var pending = Math.Max(0m, pendingContribution) + Math.Max(0m, pendingRegistration);

            rows.Add(new ContributionReportRowViewModel
            {
                SrNo = m.Sr,
                MemberName = m.Name,
                MobileNo = m.MobileNo,
                SubMember1 = sub1,
                SubMember2 = sub2,
                RegistrationFormSubmitted = m.IsRegistrationFormSubmitted,
                RegistrationFeePaid = registrationFeePaid,
                PaidByPeriod = paidByPeriod,
                ExpectedTotal = expectedGrandTotal,
                TotalPaid = totalPaid,
                TotalPendingAmount = pending
            });
        }

        var vm = new ContributionReportViewModel
        {
            ContributionStartDate = startDate,
            Periods = periods,
            Rows = rows
        };

        return View(vm);
    }

    public async Task<IActionResult> AttendanceReport(string? memberName, string? remark)
    {
        var vm = await BuildAttendanceReportModelAsync(memberName, remark, meetingTitleFilter: "General Meeting", committeeMembersOnly: false);

        ViewData["Title"] = "Attendance Report";
        ViewData["PageTitle"] = "Attendance Report";
        ViewData["BreadcrumbCurrent"] = "Attendance Report";
        ViewData["MemberName"] = memberName ?? string.Empty;
        ViewData["Remark"] = remark ?? string.Empty;
        ViewData["FilterAction"] = nameof(AttendanceReport);
        ViewData["PdfAction"] = nameof(AttendanceReportPdf);
        ViewData["ResetAction"] = nameof(AttendanceReport);
        ViewData["ShowDesignationColumn"] = false;
        return View(vm);
    }

    public async Task<IActionResult> AttendanceReportPdf(string? memberName, string? remark)
    {
        var vm = await BuildAttendanceReportModelAsync(memberName, remark, meetingTitleFilter: "General Meeting", committeeMembersOnly: false);
        var bytes = AttendanceReportPdfComposer.Generate(vm, "Attendance Report");
        var fileName = $"AttendanceReport_{DateTime.Today:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    public async Task<IActionResult> CommitteeAttendanceReport(string? memberName, string? remark)
    {
        var vm = await BuildAttendanceReportModelAsync(memberName, remark, meetingTitleFilter: "Committee Meeting", committeeMembersOnly: true);

        ViewData["Title"] = "Committee Attendance Report";
        ViewData["PageTitle"] = "Committee Attendance Report";
        ViewData["BreadcrumbCurrent"] = "Committee Attendance Report";
        ViewData["MemberName"] = memberName ?? string.Empty;
        ViewData["Remark"] = remark ?? string.Empty;
        ViewData["FilterAction"] = nameof(CommitteeAttendanceReport);
        ViewData["PdfAction"] = nameof(CommitteeAttendanceReportPdf);
        ViewData["ResetAction"] = nameof(CommitteeAttendanceReport);
        ViewData["ShowDesignationColumn"] = true;
        return View("AttendanceReport", vm);
    }

    public async Task<IActionResult> CommitteeAttendanceReportPdf(string? memberName, string? remark)
    {
        var vm = await BuildAttendanceReportModelAsync(memberName, remark, meetingTitleFilter: "Committee Meeting", committeeMembersOnly: true);
        var bytes = AttendanceReportPdfComposer.Generate(vm, "Committee Attendance Report");
        var fileName = $"CommitteeAttendanceReport_{DateTime.Today:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private async Task<AttendanceReportViewModel> BuildAttendanceReportModelAsync(
        string? memberName,
        string? remark,
        string meetingTitleFilter,
        bool committeeMembersOnly)
    {
        var membersQuery = _db.Members
            .AsNoTracking()
            .OrderBy(m => m.Sr)
            .ThenBy(m => m.Name)
            .AsQueryable();

        if (committeeMembersOnly)
            membersQuery = membersQuery.Where(m => m.IsCommiteeMember);

        var members = await membersQuery
            .Select(m => new { m.Id, m.Sr, m.Name, m.Designation, m.MobileNo })
            .ToListAsync();

        var meetings = await _db.Meetings
            .AsNoTracking()
            .Where(m =>
                string.Equals(meetingTitleFilter, "Committee Meeting", StringComparison.OrdinalIgnoreCase)
                    ? EF.Functions.ILike(m.Title, MeetingAttendanceFilters.CommitteeMeetingTitleLikePattern)
                    : EF.Functions.ILike(m.Title, MeetingAttendanceFilters.GeneralMeetingTitleLikePattern))
            .OrderBy(m => m.MeetingDate)
            .ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.MeetingDate })
            .ToListAsync();

        var meetingIds = meetings.Select(m => m.Id).ToList();
        var attendance = await _db.Attendances
            .AsNoTracking()
            .Where(a => meetingIds.Contains(a.MeetingId))
            .Select(a => new { a.MeetingId, a.MemberId, a.IsPresent })
            .ToListAsync();

        var attendanceLookup = attendance
            .GroupBy(a => new { a.MemberId, a.MeetingId })
            .ToDictionary(g => (g.Key.MemberId, g.Key.MeetingId), g => g.Last().IsPresent);

        var meetingColumns = meetings.Select(m =>
        {
            var presentCount = members.Count(member =>
                attendanceLookup.TryGetValue((member.Id, m.Id), out var isPresent) && isPresent);

            var percent = members.Count > 0
                ? presentCount * 100m / members.Count
                : 0m;

            return new AttendanceMeetingColumn
            {
                MeetingId = m.Id,
                MeetingDate = m.MeetingDate,
                PresentCount = presentCount,
                PresentPercent = percent
            };
        }).ToList();

        var rows = members.Select(member =>
        {
            var presence = meetingColumns.ToDictionary(
                mt => mt.MeetingId,
                mt => attendanceLookup.TryGetValue((member.Id, mt.MeetingId), out var isPresent) && isPresent);

            var isAbsentForAllMeetings = meetingColumns.Count > 0 && presence.Values.All(x => !x);

            return new AttendanceMemberRow
            {
                Sr = member.Sr,
                MemberName = member.Name,
                Designation = member.Designation,
                MobileNo = member.MobileNo,
                PresenceByMeeting = presence,
                Remark = isAbsentForAllMeetings ? "Absent" : string.Empty
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(memberName))
        {
            var keyword = memberName.Trim();
            rows = rows
                .Where(r => r.MemberName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(remark))
        {
            if (string.Equals(remark, "Absent", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows
                    .Where(r => string.Equals(r.Remark, "Absent", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (string.Equals(remark, "Blank", StringComparison.OrdinalIgnoreCase))
            {
                rows = rows
                    .Where(r => string.IsNullOrWhiteSpace(r.Remark))
                    .ToList();
            }
        }

        var vm = new AttendanceReportViewModel
        {
            Meetings = meetingColumns,
            Rows = rows
        };

        return vm;
    }

    public async Task<IActionResult> ContributionReportPdf()
    {
        var reportResult = await ContributionReport();
        if (reportResult is not ViewResult { Model: ContributionReportViewModel vm })
            return RedirectToAction(nameof(ContributionReport));

        var bytes = ContributionReportExportComposer.GeneratePdf(vm);
        var fileName = $"ContributionReport_{DateTime.Today:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    public async Task<IActionResult> ContributionReportJpg()
    {
        var reportResult = await ContributionReport();
        if (reportResult is not ViewResult { Model: ContributionReportViewModel vm })
            return RedirectToAction(nameof(ContributionReport));

        var bytes = ContributionReportExportComposer.GenerateJpg(vm);
        var fileName = $"ContributionReport_{DateTime.Today:yyyyMMdd}.jpg";
        return File(bytes, "image/jpeg", fileName);
    }

    public async Task<IActionResult> Statement(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeStatementDates(fromDate, toDate);
        var vm = await BuildStatementModelAsync(from, to);
        ViewData["FromDate"] = from.ToString("yyyy-MM-dd");
        ViewData["ToDate"] = to.ToString("yyyy-MM-dd");
        return View(vm);
    }

    public async Task<IActionResult> StatementPdf(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeStatementDates(fromDate, toDate);
        var vm = await BuildStatementModelAsync(from, to);
        var bytes = StatementPdfComposer.Generate(vm);
        var fileName = $"Statement_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private static (DateTime From, DateTime To) NormalizeStatementDates(DateTime? fromDate, DateTime? toDate)
    {
        var today = DateTime.Today;
        if (!fromDate.HasValue)
            fromDate = new DateTime(today.Year, today.Month, 1);
        if (!toDate.HasValue)
            toDate = today;
        if (fromDate.Value.Date > toDate.Value.Date)
            (fromDate, toDate) = (toDate, fromDate);

        return (fromDate.Value.Date, toDate.Value.Date);
    }

    private async Task<StatementReportViewModel> BuildStatementModelAsync(DateTime fromDate, DateTime toDate)
    {
        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtcExclusive = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);

        try
        {
            var openingQuery = _db.Transactions.AsNoTracking().Where(t => t.TransactionDate < fromUtc);
            var (openingCash, openingBank) = await ComputeCashAndBankAsync(openingQuery);

            var closingQuery = _db.Transactions.AsNoTracking().Where(t => t.TransactionDate < toUtcExclusive);
            var (closingCash, closingBank) = await ComputeCashAndBankAsync(closingQuery);

            var incomeTx = await _db.Transactions.AsNoTracking()
                .Where(t => t.TransactionDate >= fromUtc && t.TransactionDate < toUtcExclusive && t.Type == Transaction.TypeIncome)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TransactionDate)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var expenseTx = await _db.Transactions.AsNoTracking()
                .Where(t => t.TransactionDate >= fromUtc && t.TransactionDate < toUtcExclusive && t.Type == Transaction.TypeExpense)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TransactionDate)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var incomeLines = BuildStatementLines(incomeTx);
            var expenseLines = BuildStatementLines(expenseTx);

            var periodDeposits = await _db.Transactions.AsNoTracking()
                .Where(t =>
                    t.TransactionDate >= fromUtc &&
                    t.TransactionDate < toUtcExclusive &&
                    t.Category == Transaction.CategoryCashDepositToBank)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            var periodWithdrawals = await _db.Transactions.AsNoTracking()
                .Where(t =>
                    t.TransactionDate >= fromUtc &&
                    t.TransactionDate < toUtcExclusive &&
                    t.Category == Transaction.CategoryBankWithdrawal)
                .SumAsync(t => (decimal?)t.Amount) ?? 0m;

            return new StatementReportViewModel
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                OpeningCashInHand = openingCash,
                OpeningBankBalance = openingBank,
                IncomeLines = incomeLines,
                ExpenseLines = expenseLines,
                PeriodCashDepositToBank = periodDeposits,
                PeriodBankWithdrawal = periodWithdrawals,
                ClosingCashInHand = closingCash,
                ClosingBankBalance = closingBank
            };
        }
        catch
        {
            return new StatementReportViewModel
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                OpeningCashInHand = 0,
                OpeningBankBalance = 0,
                IncomeLines = [],
                ExpenseLines = [],
                PeriodCashDepositToBank = 0,
                PeriodBankWithdrawal = 0,
                ClosingCashInHand = 0,
                ClosingBankBalance = 0
            };
        }
    }

    /// <summary>
    /// Heads shown as a single total without per-line description (same as before for those categories).
    /// </summary>
    private static readonly HashSet<string> StatementHeadsWithoutLineDescriptions = new(StringComparer.Ordinal)
    {
        Transaction.CategoryHalfYearlyContribution,
        Transaction.CategoryMemberRegistration,
        Transaction.CategorySubMemberRegistration
    };

    private static List<StatementLine> BuildStatementLines(IReadOnlyList<Transaction> transactions)
    {
        var lines = new List<StatementLine>();
        foreach (var catGroup in transactions.GroupBy(t => t.Category).OrderBy(g => g.Key))
        {
            if (StatementHeadsWithoutLineDescriptions.Contains(catGroup.Key))
            {
                lines.Add(new StatementLine
                {
                    Head = catGroup.Key,
                    Amount = catGroup.Sum(t => t.Amount),
                    ShowDescription = false,
                    Description = null
                });
            }
            else
            {
                foreach (var t in catGroup.OrderBy(x => x.TransactionDate).ThenBy(x => x.Id))
                {
                    var desc = t.Description?.Trim();
                    lines.Add(new StatementLine
                    {
                        Head = t.Category,
                        Amount = t.Amount,
                        ShowDescription = true,
                        Description = string.IsNullOrEmpty(desc) ? null : desc
                    });
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// Matches dashboard logic: cash vs bank split by payment mode, with deposits/withdrawals moving funds between them.
    /// </summary>
    private static async Task<(decimal Cash, decimal Bank)> ComputeCashAndBankAsync(IQueryable<Transaction> q)
    {
        var cashIncome = await q.Where(t => t.Type == Transaction.TypeIncome && t.PaymentMode == Transaction.PaymentModeCash)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var cashExpense = await q.Where(t => t.Type == Transaction.TypeExpense && t.PaymentMode == Transaction.PaymentModeCash)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var deposits = await q.Where(t => t.Category == Transaction.CategoryCashDepositToBank)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var withdrawals = await q.Where(t => t.Category == Transaction.CategoryBankWithdrawal)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var onlineIncome = await q.Where(t => t.Type == Transaction.TypeIncome && t.PaymentMode == Transaction.PaymentModeOnline)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;
        var onlineExpense = await q.Where(t => t.Type == Transaction.TypeExpense && t.PaymentMode == Transaction.PaymentModeOnline)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var cash = (cashIncome - cashExpense) - deposits + withdrawals;
        var bank = (onlineIncome - onlineExpense) + deposits - withdrawals;
        return (cash, bank);
    }

    private static decimal GetContributionFeeForDate(DateTime dateUtc, IReadOnlyList<FeeSetting> feeSettings)
    {
        return GetEffectiveFee(dateUtc, feeSettings)?.ContributionFee ?? 200m;
    }

    private static decimal GetRegistrationFeeForDate(DateTime dateUtc, IReadOnlyList<FeeSetting> feeSettings)
    {
        return GetEffectiveFee(dateUtc, feeSettings)?.RegistrationFee ?? 50m;
    }

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

        // Ensure we don't include anything before the requested start date.
        var startUtc = DateTime.SpecifyKind(startDateLocal.Date, DateTimeKind.Utc);
        periods = periods.Where(p => p.EndUtcExclusive > startUtc).ToList();

        return periods;
    }
}

