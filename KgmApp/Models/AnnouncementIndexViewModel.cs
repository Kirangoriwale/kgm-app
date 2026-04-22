namespace KgmApp.Models;

public sealed class AnnouncementIndexViewModel
{
    public IReadOnlyList<Announcement> Items { get; init; } = Array.Empty<Announcement>();

    public int CurrentPage { get; init; }

    public int TotalPages { get; init; }

    public int PageSize { get; init; }
}
