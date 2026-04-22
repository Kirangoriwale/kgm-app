using KgmApp.Data;
using KgmApp.Models;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Services;

public interface IMenuPermissionService
{
    /// <summary>All menu keys with effective flags for the role (missing DB rows = deny).</summary>
    Task<IReadOnlyDictionary<string, MenuPermissionRow>> GetForRoleAsync(string roleName, CancellationToken cancellationToken = default);

    Task<bool> HasAsync(string roleName, string menuKey, MenuRouteResolver.PermissionKind kind, CancellationToken cancellationToken = default);
}

public sealed class MenuPermissionService : IMenuPermissionService
{
    private readonly AppDbContext _db;

    public MenuPermissionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, MenuPermissionRow>> GetForRoleAsync(string roleName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return EmptyDict();

        var rows = await _db.RoleMenuPermissions
            .AsNoTracking()
            .Where(r => r.RoleName == roleName)
            .ToListAsync(cancellationToken);

        var byKey = rows.ToDictionary(r => r.MenuKey, StringComparer.Ordinal);

        var result = new Dictionary<string, MenuPermissionRow>(StringComparer.Ordinal);
        foreach (var key in MenuKeys.All)
        {
            result[key] = byKey.TryGetValue(key, out var entity)
                ? MenuPermissionRow.FromEntity(entity)
                : MenuPermissionRow.DenyAll;
        }

        return result;
    }

    public async Task<bool> HasAsync(string roleName, string menuKey, MenuRouteResolver.PermissionKind kind,
        CancellationToken cancellationToken = default)
    {
        var map = await GetForRoleAsync(roleName, cancellationToken);
        if (!map.TryGetValue(menuKey, out var row))
            return false;
        return MenuRouteResolver.Allows(kind, row);
    }

    private static IReadOnlyDictionary<string, MenuPermissionRow> EmptyDict()
    {
        return MenuKeys.All.ToDictionary(k => k, _ => MenuPermissionRow.DenyAll, StringComparer.Ordinal);
    }
}
