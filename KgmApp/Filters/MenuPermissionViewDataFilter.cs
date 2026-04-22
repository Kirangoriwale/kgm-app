using KgmApp.Models;
using KgmApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KgmApp.Filters;

/// <summary>Injects MenuPerms into ViewData for every view (sidebar + buttons).</summary>
public sealed class MenuPermissionViewDataFilter : IAsyncResultFilter
{
    private readonly IMenuPermissionService _menuPermissions;

    public MenuPermissionViewDataFilter(IMenuPermissionService menuPermissions)
    {
        _menuPermissions = menuPermissions;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var role = context.HttpContext.Session.GetString("Role");
        IReadOnlyDictionary<string, MenuPermissionRow> map =
            string.IsNullOrWhiteSpace(role)
                ? MenuKeys.All.ToDictionary(k => k, _ => MenuPermissionRow.DenyAll, StringComparer.Ordinal)
                : await _menuPermissions.GetForRoleAsync(role);

        void Merge(ViewDataDictionary vd)
        {
            vd["MenuPerms"] = map;
        }

        switch (context.Result)
        {
            case ViewResult vr:
                Merge(vr.ViewData);
                break;
            case PartialViewResult pvr:
                Merge(pvr.ViewData);
                break;
        }

        await next();
    }
}
