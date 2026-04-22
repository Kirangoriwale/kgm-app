using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class Member
{
    public int Id { get; set; }

    [Display(Name = "Sr.")]
    public int Sr { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(256)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits.")]
    [Display(Name = "Mobile No")]
    public string MobileNo { get; set; } = string.Empty;

    [StringLength(256)]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email ID")]
    public string EmailId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    [StringLength(512)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    [Display(Name = "Registration Form Submitted")]
    public bool IsRegistrationFormSubmitted { get; set; }

    [Display(Name = "Is Commitee Member")]
    public bool IsCommiteeMember { get; set; }

    [Display(Name = "Restrict Login")]
    public bool RestrictLogin { get; set; }

    [StringLength(256)]
    [Display(Name = "Login Password")]
    public string LoginPassword { get; set; } = string.Empty;

    /// <summary>
    /// When true, user must change password after login before accessing the app.
    /// </summary>
    [Display(Name = "First login (change password)")]
    public bool IsFirstLogin { get; set; } = true;

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(32)]
    [Display(Name = "Role")]
    public string Role { get; set; } = "Member";

    [StringLength(256)]
    [Display(Name = "Designation")]
    public string Designation { get; set; } = string.Empty;

    public ICollection<SubMember> SubMembers { get; set; } = new List<SubMember>();
}
