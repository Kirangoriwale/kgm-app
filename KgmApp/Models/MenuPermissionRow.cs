namespace KgmApp.Models;

/// <summary>Per-menu flags exposed to views (ViewData["MenuPerms"]).</summary>
public sealed class MenuPermissionRow
{
    public bool CanView { get; init; }
    public bool CanAdd { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }

    public static MenuPermissionRow DenyAll { get; } = new();

    public static MenuPermissionRow FromEntity(RoleMenuPermission? p)
    {
        if (p == null)
            return DenyAll;
        return new MenuPermissionRow
        {
            CanView = p.CanView,
            CanAdd = p.CanAdd,
            CanEdit = p.CanEdit,
            CanDelete = p.CanDelete
        };
    }
}
