namespace KgmApp.Models;

public sealed class SidebarMenuViewModel
{
    public required IReadOnlyDictionary<string, MenuPermissionRow> Perms { get; init; }
    public required string CurrentController { get; init; }

    public required string CurrentAction { get; init; }

    /// <summary>Legacy dashboard route target.</summary>
    public required string DashboardAction { get; init; }

    public bool IsDashboardActive =>
        string.Equals(CurrentController, "Home", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(CurrentAction, "Index", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CurrentAction, "Privacy", StringComparison.OrdinalIgnoreCase));

    public bool IsMyDashboardActive =>
        string.Equals(CurrentController, "Home", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(CurrentAction, "MyDashboard", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CurrentAction, "MemberDashboard", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CurrentAction, "MemberPaidDetails", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CurrentAction, "MemberPendingDetails", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(CurrentAction, "MemberGeneralMeetingAttendance", StringComparison.OrdinalIgnoreCase));

    public bool Can(string menuKey) =>
        Perms.TryGetValue(menuKey, out var r) && r.CanView;

    public bool ShowReportsSection =>
        Can(MenuKeys.ReportContribution) ||
        Can(MenuKeys.ReportStatement) ||
        Can(MenuKeys.ReportAttendance) ||
        Can(MenuKeys.ReportCommitteeAttendance);

    public bool ShowSettingsSection =>
        Can(MenuKeys.SettingsUser) ||
        Can(MenuKeys.SettingsFees) ||
        Can(MenuKeys.SettingsChangePassword) ||
        Can(MenuKeys.SettingsLoginLogs) ||
        Can(MenuKeys.SettingsRulesRegulations) ||
        Can(MenuKeys.SettingsUserRights);

    public bool IsReportsOpen => string.Equals(CurrentController, "Report", StringComparison.OrdinalIgnoreCase);

    public bool IsSettingsOpen =>
        string.Equals(CurrentController, "User", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CurrentController, "FeeSetting", StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(CurrentController, "Account", StringComparison.OrdinalIgnoreCase) &&
         string.Equals(CurrentAction, "SettingsChangePassword", StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(CurrentController, "LoginLogs", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CurrentController, "RulesRegulations", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CurrentController, "UserRights", StringComparison.OrdinalIgnoreCase);
}
