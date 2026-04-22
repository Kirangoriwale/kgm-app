namespace KgmApp.Models.Reports;

public sealed class StatementReportViewModel
{
    public required DateTime FromDate { get; init; }

    public required DateTime ToDate { get; init; }

    public required decimal OpeningCashInHand { get; init; }

    public required decimal OpeningBankBalance { get; init; }

    public required IReadOnlyList<StatementLine> IncomeLines { get; init; }

    public required IReadOnlyList<StatementLine> ExpenseLines { get; init; }

    /// <summary>Total "Cash Deposit to Bank" in the period (cash → bank).</summary>
    public required decimal PeriodCashDepositToBank { get; init; }

    /// <summary>Total "Bank Withdrawal" in the period (bank → cash).</summary>
    public required decimal PeriodBankWithdrawal { get; init; }

    public required decimal ClosingCashInHand { get; init; }

    public required decimal ClosingBankBalance { get; init; }

    public decimal TotalIncomeInPeriod => IncomeLines.Sum(x => x.Amount);

    public decimal TotalExpenseInPeriod => ExpenseLines.Sum(x => x.Amount);

    public decimal OpeningTotalCash => OpeningCashInHand + OpeningBankBalance;

    public decimal ClosingTotalCash => ClosingCashInHand + ClosingBankBalance;
}

/// <summary>
/// One row on the statement. For some heads (registration, contribution) only an aggregate line is shown without per-line description.
/// </summary>
public sealed class StatementLine
{
    public required string Head { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>When false, description is not shown (head is one aggregate line).</summary>
    public required bool ShowDescription { get; init; }

    public string? Description { get; init; }
}
