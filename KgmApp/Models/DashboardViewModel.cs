namespace KgmApp.Models;

public class DashboardViewModel
{
    public int TotalMembers { get; set; }

    public int TotalSubMembers { get; set; }

    /// <summary>Members + SubMembers with registration form submitted.</summary>
    public int RegistrationFormsSubmittedCount { get; set; }

    public decimal CashInHand { get; set; }

    public decimal BankBalance { get; set; }
}
