namespace KgmApp.Models.Meetings;

public sealed class MeetingAttendanceViewModel
{
    public required int MeetingId { get; init; }

    public required string MeetingTitle { get; init; }

    public required DateTime MeetingDate { get; init; }

    public required string MeetingLocation { get; init; }

    public required string MinutesOfMeeting { get; init; }

    public required IReadOnlyList<MemberAttendanceRow> Members { get; init; }
}

public sealed class MemberAttendanceRow
{
    public int? AttendanceId { get; init; }

    public required int MemberId { get; init; }

    public required int Sr { get; init; }

    public required string MemberName { get; init; }

    public required string MobileNo { get; init; }

    public required bool IsPresent { get; init; }
}

public sealed class MeetingAttendanceSubmitModel
{
    public int MeetingId { get; set; }

    public List<MemberAttendanceSubmitRow> Members { get; set; } = [];
}

public sealed class MemberAttendanceSubmitRow
{
    public int MemberId { get; set; }

    public bool IsPresent { get; set; }
}

