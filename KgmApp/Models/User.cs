using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(256)]
    public string Password { get; set; } = string.Empty;

    [StringLength(256)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(50)]
    public string Role { get; set; } = "User";
}
