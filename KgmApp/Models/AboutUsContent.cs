using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public sealed class AboutUsContent
{
    public int Id { get; set; }

    [Required]
    public string ContentHtml { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
