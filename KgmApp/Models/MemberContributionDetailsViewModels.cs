namespace KgmApp.Models;

public sealed class MemberContributionPaidLine
{
    public DateTime TransactionDate { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class MemberRegistrationPaidLine
{
    public string PartyLabel { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public decimal Amount { get; init; }
}

public sealed class MemberContributionPendingPeriodLine
{
    public string PeriodLabel { get; init; } = string.Empty;
    public decimal ExpectedFee { get; init; }
    public decimal PaidInPeriod { get; init; }
}

public sealed class MemberPaidDetailsViewModel
{
    public string MemberName { get; init; } = string.Empty;

    public IReadOnlyList<MemberContributionPaidLine> ContributionPayments { get; init; } =
        Array.Empty<MemberContributionPaidLine>();

    public decimal ContributionsPaidTotal { get; init; }

    public IReadOnlyList<MemberRegistrationPaidLine> RegistrationPayments { get; init; } =
        Array.Empty<MemberRegistrationPaidLine>();

    public decimal RegistrationPaidTotal { get; init; }

    public decimal GrandTotal { get; init; }
}

public sealed class MemberPendingDetailsViewModel
{
    public string MemberName { get; init; } = string.Empty;

    public decimal ExpectedContributionTotal { get; init; }
    public decimal PaidContributionTotal { get; init; }
    public decimal PendingContribution { get; init; }

    public IReadOnlyList<MemberContributionPendingPeriodLine> ContributionByPeriod { get; init; } =
        Array.Empty<MemberContributionPendingPeriodLine>();

    public decimal RegistrationFeePerPerson { get; init; }
    public int SubMemberCount { get; init; }
    public decimal ExpectedRegistrationTotal { get; init; }
    public decimal PaidMemberRegistration { get; init; }
    public decimal PaidSubMemberRegistration { get; init; }
    public decimal RegistrationPaidTotal { get; init; }
    public decimal PendingRegistration { get; init; }

    public decimal GrandPendingTotal { get; init; }
}
