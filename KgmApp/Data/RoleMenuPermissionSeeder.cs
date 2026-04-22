using KgmApp.Models;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Data;

public static class RoleMenuPermissionSeeder
{
    private static readonly string[] Roles = ["Admin", "Treasurer", "Secretary", "Committee", "Member"];

    /// <summary>Inserts default permission rows when missing (idempotent per role+menu).</summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var role in Roles)
        {
            foreach (var menuKey in MenuKeys.All)
            {
                var exists = await db.RoleMenuPermissions
                    .AnyAsync(r => r.RoleName == role && r.MenuKey == menuKey, cancellationToken);
                if (exists)
                    continue;

                var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
                var isDashboard = menuKey == MenuKeys.HomeDashboard;
                var isMyDashboard = menuKey == MenuKeys.MyDashboard;
                var isAboutUs = menuKey == MenuKeys.AboutUs;
                var isCommitteeMembers = menuKey == MenuKeys.CommitteeMembers;
                var isAnnouncements = menuKey == MenuKeys.Announcements;
                var isSuggestions = menuKey == MenuKeys.Suggestions;
                var isChangePassword = menuKey == MenuKeys.SettingsChangePassword;
                var isRulesRegulations = menuKey == MenuKeys.SettingsRulesRegulations;

                db.RoleMenuPermissions.Add(new RoleMenuPermission
                {
                    RoleName = role,
                    MenuKey = menuKey,
                    CanView = isAdmin || isDashboard || isMyDashboard || isAboutUs || isCommitteeMembers || isAnnouncements || isSuggestions || isChangePassword || isRulesRegulations,
                    CanAdd = isAdmin || isSuggestions,
                    CanEdit = isAdmin,
                    CanDelete = isAdmin
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
