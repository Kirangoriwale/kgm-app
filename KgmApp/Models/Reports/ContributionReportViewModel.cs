namespace KgmApp.Models.Reports;

public sealed class ContributionReportViewModel
{
    public required DateTime ContributionStartDate { get; init; }
    public required IReadOnlyList<HalfYearPeriod> Periods { get; init; }
    public required IReadOnlyList<ContributionReportRowViewModel> Rows { get; init; }

    public int TotalMembers => Rows.Count;
    public int PaidCount => Rows.Count(r => r.TotalPendingAmount <= 0);
    public int PendingCount => Rows.Count(r => r.TotalPendingAmount > 0);
}

public sealed class HalfYearPeriod
{
    public required string FinancialYearLabel { get; init; } // e.g. "2025-26"
    public required string PeriodLabel { get; init; }        // e.g. "Apr–Sep"

    // UTC boundaries to match PostgreSQL timestamptz handling in Transactions.
    public required DateTime StartUtc { get; init; }          // inclusive
    public required DateTime EndUtcExclusive { get; init; }   // exclusive

    public string Key => $"{FinancialYearLabel}:{PeriodLabel}";
}

public sealed class ContributionReportRowViewModel
{
    public required int SrNo { get; init; }
    public required string MemberName { get; init; }
    public required string MobileNo { get; init; }
    public required string SubMember1 { get; init; }
    public required string SubMember2 { get; init; }

    public required bool RegistrationFormSubmitted { get; init; }
    public required decimal RegistrationFeePaid { get; init; }

    // Keyed by HalfYearPeriod.Key
    public required IReadOnlyDictionary<string, decimal> PaidByPeriod { get; init; }

    public required decimal ExpectedTotal { get; init; }

    public required decimal TotalPaid { get; init; }

    public required decimal TotalPendingAmount { get; init; }
}

