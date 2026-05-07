namespace KgmApp.Models;

/// <summary>Stable keys for sidebar menus and permission rows.</summary>
public static class MenuKeys
{
    public const string HomeDashboard = "Home_Dashboard";
    public const string MyDashboard = "Home_MyDashboard";
    public const string AboutUs = "Home_AboutUs";
    public const string AboutDeveloper = "Home_AboutDeveloper";
    public const string CommitteeMembers = "Home_CommitteeMembers";
    public const string Members = "Members";
    public const string Transactions = "Transactions";
    public const string Meetings = "Meetings";
    public const string Announcements = "Announcements";
    public const string Suggestions = "Suggestions";
    public const string ReportContribution = "Report_Contribution";
    public const string ReportStatement = "Report_Statement";
    public const string ReportAttendance = "Report_Attendance";
    public const string ReportCommitteeAttendance = "Report_CommitteeAttendance";
    public const string SettingsUser = "Settings_User";
    public const string SettingsFees = "Settings_Fees";
    public const string SettingsChangePassword = "Settings_ChangePassword";
    public const string SettingsLoginLogs = "Settings_LoginLogs";
    public const string SettingsRulesRegulations = "Settings_RulesRegulations";
    public const string SettingsUserRights = "Settings_UserRights";

    public static IReadOnlyList<string> All { get; } =
    [
        HomeDashboard,
        MyDashboard,
        AboutUs,
        AboutDeveloper,
        CommitteeMembers,
        Members,
        Transactions,
        Meetings,
        Announcements,
        Suggestions,
        ReportContribution,
        ReportStatement,
        ReportAttendance,
        ReportCommitteeAttendance,
        SettingsUser,
        SettingsFees,
        SettingsChangePassword,
        SettingsLoginLogs,
        SettingsRulesRegulations,
        SettingsUserRights
    ];
}
