namespace KgmApp.Models;

public sealed class MemberGeneralMeetingAttendanceViewModel
{
    public string MemberName { get; init; } = string.Empty;

    public IReadOnlyList<MemberGeneralMeetingAttendanceRowViewModel> Rows { get; init; } =
        Array.Empty<MemberGeneralMeetingAttendanceRowViewModel>();
}

public sealed class MemberGeneralMeetingAttendanceRowViewModel
{
    public DateTime MeetingDate { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public string MinutesOfMeeting { get; init; } = string.Empty;

    /// <summary>Present, Absent, or Not marked.</summary>
    public string PresenceText { get; init; } = string.Empty;
}
