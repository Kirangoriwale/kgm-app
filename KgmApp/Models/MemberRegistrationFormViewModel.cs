using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class MemberRegistrationFormViewModel : IValidatableObject
{
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Mobile No")]
    public string MobileNo { get; set; } = string.Empty;

    [Display(Name = "Terms")]
    public string? Terms { get; set; }

    [Display(Name = "Email ID")]
    [Required(ErrorMessage = "Email ID is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(256)]
    public string? EmailId { get; set; }

    [Display(Name = "Address")]
    [Required(ErrorMessage = "Address is required.")]
    [StringLength(512)]
    public string? Address { get; set; }

    [Display(Name = "Date of Birth")]
    [Required(ErrorMessage = "Date of Birth is required.")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Aadhaar No")]
    [Required(ErrorMessage = "Aadhaar No is required.")]
    [RegularExpression(@"^\d{12}$", ErrorMessage = "Aadhaar No must be exactly 12 digits.")]
    [StringLength(12)]
    public string? AadhaarNo { get; set; }

    [Display(Name = "Education")]
    [Required(ErrorMessage = "Education is required.")]
    [StringLength(256)]
    public string? Education { get; set; }

    [Display(Name = "Business/Job")]
    [Required(ErrorMessage = "Business/Job is required.")]
    [StringLength(256)]
    public string? BusinessOrJob { get; set; }

    [Display(Name = "I have read and accept the Rules & Regulations.")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "Please accept Terms to continue.")]
    public bool TermsAcceptYN { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsBlankOrDash(EmailId))
            yield return new ValidationResult("Email ID cannot be blank or '-'.", [nameof(EmailId)]);
        if (IsBlankOrDash(Address))
            yield return new ValidationResult("Address cannot be blank or '-'.", [nameof(Address)]);
        if (IsBlankOrDash(AadhaarNo))
            yield return new ValidationResult("Aadhaar No cannot be blank or '-'.", [nameof(AadhaarNo)]);
        if (IsBlankOrDash(Education))
            yield return new ValidationResult("Education cannot be blank or '-'.", [nameof(Education)]);
        if (IsBlankOrDash(BusinessOrJob))
            yield return new ValidationResult("Business/Job cannot be blank or '-'.", [nameof(BusinessOrJob)]);
    }

    private static bool IsBlankOrDash(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized == "-";
    }
}
