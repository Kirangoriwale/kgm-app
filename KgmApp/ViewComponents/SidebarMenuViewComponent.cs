using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace KgmApp.ViewComponents;

public sealed class SidebarMenuViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var vd = ViewContext.ViewData;
        var perms = vd["MenuPerms"] as IReadOnlyDictionary<string, MenuPermissionRow>;
        if (perms == null)
        {
            perms = MenuKeys.All.ToDictionary(k => k, _ => MenuPermissionRow.DenyAll, StringComparer.Ordinal);
        }

        var controller = ViewContext.RouteData.Values["controller"]?.ToString() ?? "";
        var action = ViewContext.RouteData.Values["action"]?.ToString() ?? "";
        var dashboardAction = "Index";

        var vm = new SidebarMenuViewModel
        {
            Perms = perms,
            CurrentController = controller,
            CurrentAction = action,
            DashboardAction = dashboardAction
        };

        return View(vm);
    }
}
