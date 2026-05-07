namespace KgmApp.Models;

public sealed record MenuDefinition(string Key, string DisplayName, int SortOrder);

/// <summary>All menus shown on User Rights screen (order matches sidebar groups).</summary>
public static class MenuCatalog
{
    public static IReadOnlyList<MenuDefinition> All { get; } =
    [
        new(MenuKeys.HomeDashboard, "Mandal Dashboard", 1),
        new(MenuKeys.MyDashboard, "My Dashboard", 2),
        new(MenuKeys.AboutUs, "About Us", 3),
        new(MenuKeys.CommitteeMembers, "Committee Members", 5),
        new(MenuKeys.Members, "Members", 10),
        new(MenuKeys.Transactions, "Transactions", 20),
        new(MenuKeys.Meetings, "Meetings", 30),
        new(MenuKeys.Announcements, "Announcements", 35),
        new(MenuKeys.Suggestions, "Suggestions", 36),
        new(MenuKeys.ReportContribution, "Report — Contribution", 40),
        new(MenuKeys.ReportStatement, "Report — Statement", 41),
        new(MenuKeys.ReportAttendance, "Report — Attendance", 42),
        new(MenuKeys.ReportCommitteeAttendance, "Report — Committee Attendance", 43),
        new(MenuKeys.SettingsUser, "Settings — User", 50),
        new(MenuKeys.SettingsFees, "Settings — Fees", 51),
        new(MenuKeys.SettingsChangePassword, "Settings — Change Password", 52),
        new(MenuKeys.SettingsLoginLogs, "Settings — Login Logs", 53),
        new(MenuKeys.SettingsRulesRegulations, "Settings — Rules & Regulations", 54),
        new(MenuKeys.SettingsUserRights, "Settings — User Rights", 55),
        new(MenuKeys.AboutDeveloper, "About Developer", 99)
    ];
}
