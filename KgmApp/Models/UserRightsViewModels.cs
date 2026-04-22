namespace KgmApp.Models;

public sealed class MenuPermissionEditItem
{
    public string MenuKey { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}

public sealed class UserRightsIndexViewModel
{
    public string SelectedRole { get; set; } = "Admin";

    public List<MenuPermissionEditItem> Items { get; set; } = [];
}
