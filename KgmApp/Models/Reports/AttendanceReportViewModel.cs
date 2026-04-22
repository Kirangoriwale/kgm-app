namespace KgmApp.Models.Reports;

public sealed class AttendanceReportViewModel
{
    public required IReadOnlyList<AttendanceMeetingColumn> Meetings { get; init; }

    public required IReadOnlyList<AttendanceMemberRow> Rows { get; init; }

    public int TotalMembers => Rows.Count;
}

public sealed class AttendanceMeetingColumn
{
    public required int MeetingId { get; init; }

    public required DateTime MeetingDate { get; init; }

    public required int PresentCount { get; init; }

    public required decimal PresentPercent { get; init; }
}

public sealed class AttendanceMemberRow
{
    public required int Sr { get; init; }

    public required string MemberName { get; init; }

    public required string Designation { get; init; }

    public required string MobileNo { get; init; }

    // Key: MeetingId, Value: true if present.
    public required IReadOnlyDictionary<int, bool> PresenceByMeeting { get; init; }

    public required string Remark { get; init; }
}

