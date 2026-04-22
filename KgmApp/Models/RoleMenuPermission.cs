using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public class RoleMenuPermission
{
    public int Id { get; set; }

    [Required]
    [StringLength(32)]
    public string RoleName { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string MenuKey { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}
