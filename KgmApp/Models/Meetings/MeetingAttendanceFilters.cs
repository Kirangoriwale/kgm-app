namespace KgmApp.Models.Meetings;

/// <summary>Title patterns for meeting lists — keep in sync with attendance report queries in <c>ReportController</c>.</summary>
public static class MeetingAttendanceFilters
{
    public const string GeneralMeetingTitleLikePattern = "%general%";

    public const string CommitteeMeetingTitleLikePattern = "%committee%";
}
