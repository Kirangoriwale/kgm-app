using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public sealed class Announcement
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Announcement content is required.")]
    public string ContentHtml { get; set; } = string.Empty;

    [StringLength(256)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
