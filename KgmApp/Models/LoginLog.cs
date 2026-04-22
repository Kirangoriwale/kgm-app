using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public sealed class LoginLog
{
    public int Id { get; set; }

    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(20)]
    public string MobileNo { get; set; } = string.Empty;

    public DateTime LoginTimeUtc { get; set; } = DateTime.UtcNow;
}
