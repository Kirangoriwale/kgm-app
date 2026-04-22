using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class SubMember
{
    public int Id { get; set; }

    [Display(Name = "Sr. No.")]
    public int SrNo { get; set; }

    public int MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    [StringLength(256)]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email ID")]
    public string EmailId { get; set; } = string.Empty;

    public string Relation { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [Display(Name = "Registration Form Submitted")]
    public bool IsRegistrationFormSubmitted { get; set; }

    public string Designation { get; set; } = string.Empty;

    [ValidateNever]
    public Member? Member { get; set; }
}
