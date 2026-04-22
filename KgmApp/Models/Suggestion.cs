using System.ComponentModel.DataAnnotations;

namespace KgmApp.Models;

public sealed class Suggestion
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    [StringLength(256)]
    public string MemberName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Suggestion text is required.")]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
