using KgmApp.Models;

namespace KgmApp.Services;

/// <summary>Maps MVC controller/action to a menu key and required permission.</summary>
public static class MenuRouteResolver
{
    public enum PermissionKind
    {
        View,
        Add,
        Edit,
        Delete
    }

    public readonly record struct Resolved(string MenuKey, PermissionKind Kind);

    /// <summary>Returns null if the route does not require a menu permission check.</summary>
    public static Resolved? Resolve(string? controller, string? action)
    {
        if (string.IsNullOrWhiteSpace(controller) || string.IsNullOrWhiteSpace(action))
            return null;

        var c = controller.Trim();
        var a = action.Trim();

        if (string.Equals(c, "Account", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(a, "SettingsChangePassword", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.SettingsChangePassword, PermissionKind.View);
            if (string.Equals(a, "SaveSettingsChangePassword", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.SettingsChangePassword, PermissionKind.View);
            return null;
        }

        if (string.Equals(c, "Home", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(a, "Error", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.Equals(a, "Index", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "Privacy", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.HomeDashboard, PermissionKind.View);
            if (string.Equals(a, "AboutUs", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.AboutUs, PermissionKind.View);
            if (string.Equals(a, "SaveAboutUs", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.AboutUs, PermissionKind.Edit);
            if (string.Equals(a, "CommitteeMembers", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.CommitteeMembers, PermissionKind.View);
            if (string.Equals(a, "MyDashboard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "MemberDashboard", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "MemberPaidDetails", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "MemberPendingDetails", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "MemberGeneralMeetingAttendance", StringComparison.OrdinalIgnoreCase))
                return new Resolved(MenuKeys.MyDashboard, PermissionKind.View);
            return null;
        }

        if (string.Equals(c, "Member", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" or "Details" => new Resolved(MenuKeys.Members, PermissionKind.View),
                "Create" or "ImportExcel" or "DownloadImportTemplate" => new Resolved(MenuKeys.Members, PermissionKind.Add),
                "Edit" => new Resolved(MenuKeys.Members, PermissionKind.Edit),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.Members, PermissionKind.Delete),
                "AddSubMember" => new Resolved(MenuKeys.Members, PermissionKind.Add),
                "EditSubMember" => new Resolved(MenuKeys.Members, PermissionKind.Edit),
                "DeleteSubMember" => new Resolved(MenuKeys.Members, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.Members, PermissionKind.View)
            };
        }

        if (string.Equals(c, "SubMembers", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.Members, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.Members, PermissionKind.Add),
                _ => new Resolved(MenuKeys.Members, PermissionKind.View)
            };
        }

        if (string.Equals(c, "Transaction", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.Transactions, PermissionKind.View),
                "Create" or "ImportExcel" or "DownloadImportTemplate" => new Resolved(MenuKeys.Transactions, PermissionKind.Add),
                "Edit" => new Resolved(MenuKeys.Transactions, PermissionKind.Edit),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.Transactions, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.Transactions, PermissionKind.View)
            };
        }

        if (string.Equals(c, "Meeting", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.Meetings, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.Meetings, PermissionKind.Add),
                "Edit" => new Resolved(MenuKeys.Meetings, PermissionKind.Edit),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.Meetings, PermissionKind.Delete),
                "MarkAttendance" or "DeleteAttendance" => new Resolved(MenuKeys.Meetings, PermissionKind.Edit),
                _ => new Resolved(MenuKeys.Meetings, PermissionKind.View)
            };
        }

        if (string.Equals(c, "Announcement", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.Announcements, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.Announcements, PermissionKind.Add),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.Announcements, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.Announcements, PermissionKind.View)
            };
        }

        if (string.Equals(c, "Suggestion", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.Suggestions, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.Suggestions, PermissionKind.Add),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.Suggestions, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.Suggestions, PermissionKind.View)
            };
        }

        if (string.Equals(c, "Report", StringComparison.OrdinalIgnoreCase))
        {
            var mk = a switch
            {
                "ContributionReport" or "ContributionReportPdf" or "ContributionReportJpg" => MenuKeys.ReportContribution,
                "Statement" or "StatementPdf" or "StatementJpg" => MenuKeys.ReportStatement,
                "AttendanceReport" or "AttendanceReportPdf" => MenuKeys.ReportAttendance,
                "CommitteeAttendanceReport" or "CommitteeAttendanceReportPdf" => MenuKeys.ReportCommitteeAttendance,
                _ => MenuKeys.ReportContribution
            };
            return new Resolved(mk, PermissionKind.View);
        }

        if (string.Equals(c, "User", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.SettingsUser, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.SettingsUser, PermissionKind.Add),
                "Edit" => new Resolved(MenuKeys.SettingsUser, PermissionKind.Edit),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.SettingsUser, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.SettingsUser, PermissionKind.View)
            };
        }

        if (string.Equals(c, "FeeSetting", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.SettingsFees, PermissionKind.View),
                "Create" => new Resolved(MenuKeys.SettingsFees, PermissionKind.Add),
                "Edit" => new Resolved(MenuKeys.SettingsFees, PermissionKind.Edit),
                "Delete" or "DeleteConfirmed" => new Resolved(MenuKeys.SettingsFees, PermissionKind.Delete),
                _ => new Resolved(MenuKeys.SettingsFees, PermissionKind.View)
            };
        }

        if (string.Equals(c, "LoginLogs", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.SettingsLoginLogs, PermissionKind.View),
                _ => new Resolved(MenuKeys.SettingsLoginLogs, PermissionKind.View)
            };
        }

        if (string.Equals(c, "RulesRegulations", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.SettingsRulesRegulations, PermissionKind.View),
                "Save" => new Resolved(MenuKeys.SettingsRulesRegulations, PermissionKind.Edit),
                _ => new Resolved(MenuKeys.SettingsRulesRegulations, PermissionKind.View)
            };
        }

        if (string.Equals(c, "UserRights", StringComparison.OrdinalIgnoreCase))
        {
            return a switch
            {
                "Index" => new Resolved(MenuKeys.SettingsUserRights, PermissionKind.View),
                "Save" => new Resolved(MenuKeys.SettingsUserRights, PermissionKind.Edit),
                _ => new Resolved(MenuKeys.SettingsUserRights, PermissionKind.View)
            };
        }

        return null;
    }

    public static bool Allows(PermissionKind kind, MenuPermissionRow row) =>
        kind switch
        {
            PermissionKind.View => row.CanView,
            PermissionKind.Add => row.CanAdd,
            PermissionKind.Edit => row.CanEdit,
            PermissionKind.Delete => row.CanDelete,
            _ => false
        };
}
