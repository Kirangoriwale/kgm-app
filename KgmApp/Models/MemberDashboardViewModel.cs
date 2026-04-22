namespace KgmApp.Models;

public sealed class MemberDashboardViewModel
{
    public string MemberName { get; init; } = string.Empty;

    public bool IsMemberRegistrationSubmitted { get; init; }

    public IReadOnlyList<MemberDashboardSubMemberInfoViewModel> SubMembers { get; init; } =
        Array.Empty<MemberDashboardSubMemberInfoViewModel>();

    /// <summary>Contribution + registration fees paid (aligned with contribution report).</summary>
    public decimal TotalPaid { get; init; }

    /// <summary>Outstanding contribution + registration (aligned with contribution report).</summary>
    public decimal TotalPending { get; init; }

    /// <summary>UPI deep link with <see cref="TotalPending"/> as amount; null if not configured or nothing to pay.</summary>
    public string? UpiPayPendingHref { get; init; }

    /// <summary>General meetings (title contains "general", same rule as attendance report) where this member is marked present.</summary>
    public int GeneralMeetingsPresentCount { get; init; }

    /// <summary>Total general meetings held (same filter as attendance report).</summary>
    public int GeneralMeetingsTotalCount { get; init; }

    /// <summary>Earliest general meeting with <see cref="UpcomingGeneralMeetingDate"/> after today’s date.</summary>
    public bool HasUpcomingGeneralMeeting { get; init; }

    public DateTime? UpcomingGeneralMeetingDate { get; init; }

    public string UpcomingGeneralMeetingTitle { get; init; } = string.Empty;

    public string UpcomingGeneralMeetingLocation { get; init; } = string.Empty;
}

public sealed class MemberDashboardSubMemberInfoViewModel
{
    public string Name { get; init; } = string.Empty;

    public bool IsRegistrationSubmitted { get; init; }
}
