using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KgmApp.Models;

public class Transaction
{
    public const string TypeIncome = "Income";
    public const string TypeExpense = "Expense";
    public const string TypeTransfer = "Transfer";

    public const string CategoryMemberRegistration = "Member Registration";
    public const string CategorySubMemberRegistration = "SubMember Registration";
    public const string CategoryHalfYearlyContribution = "HalfYearly Contribution";
    public const string CategoryDonation = "Donation";
    public const string CategoryExpense = "Expense";
    public const string CategoryBankCharges = "Bank Charges";
    public const string CategoryBankInterest = "Bank Interest";
    public const string CategoryCashDepositToBank = "Cash Deposit to Bank";
    public const string CategoryBankWithdrawal = "Bank Withdrawal";

    public const string PaymentModeCash = "Cash";
    public const string PaymentModeOnline = "Online";

    public int Id { get; set; }

    [Display(Name = "Transaction Date")]
    [DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(20)]
    public string Type { get; set; } = TypeIncome;

    [Required]
    [StringLength(64)]
    public string Category { get; set; } = CategoryExpense;

    public int? MemberId { get; set; }

    public int? SubMemberId { get; set; }

    [StringLength(256)]
    [Display(Name = "Donor Name")]
    public string? DonorName { get; set; }

    [StringLength(32)]
    [Display(Name = "Donor Mobile")]
    public string? DonorMobile { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }

    [Required]
    [Display(Name = "Payment Mode")]
    [StringLength(20)]
    public string PaymentMode { get; set; } = PaymentModeCash;

    [StringLength(1024)]
    public string Description { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Created By")]
    public string CreatedBy { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Modified By")]
    public string ModifiedBy { get; set; } = string.Empty;

    [ValidateNever]
    public Member? Member { get; set; }

    [ValidateNever]
    public SubMember? SubMember { get; set; }
}
