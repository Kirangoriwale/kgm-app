using KgmApp.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KgmApp.Helpers;

public static class ViewMenu
{
    public static bool Can(ViewDataDictionary viewData, string menuKey, string permission)
    {
        if (viewData["MenuPerms"] is not IReadOnlyDictionary<string, MenuPermissionRow> map)
            return false;
        if (!map.TryGetValue(menuKey, out var row))
            return false;
        return permission switch
        {
            "View" => row.CanView,
            "Add" => row.CanAdd,
            "Edit" => row.CanEdit,
            "Delete" => row.CanDelete,
            _ => false
        };
    }
}
