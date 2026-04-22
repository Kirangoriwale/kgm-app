using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile number must be exactly 10 digits.")]
    [Display(Name = "Mobile No")]
    public string MobileNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
