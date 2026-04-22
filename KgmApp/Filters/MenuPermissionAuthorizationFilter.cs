using KgmApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KgmApp.Filters;

/// <summary>Enforces menu rights for controller actions (session Role).</summary>
public sealed class MenuPermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IMenuPermissionService _menuPermissions;

    public MenuPermissionAuthorizationFilter(IMenuPermissionService menuPermissions)
    {
        _menuPermissions = menuPermissions;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var controller = http.Request.RouteValues["controller"]?.ToString();
        var action = http.Request.RouteValues["action"]?.ToString();

        var resolved = MenuRouteResolver.Resolve(controller, action);
        if (resolved == null)
            return;

        var role = http.Session.GetString("Role");
        if (string.IsNullOrWhiteSpace(role))
        {
            context.Result = new RedirectResult("/Account/Login");
            return;
        }

        var ok = await _menuPermissions.HasAsync(role, resolved.Value.MenuKey, resolved.Value.Kind);
        if (ok)
            return;

        var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        tempData["LayoutError"] = "You do not have permission to access that screen.";

        context.Result = new RedirectToActionResult("Index", "Home", null);
    }
}
