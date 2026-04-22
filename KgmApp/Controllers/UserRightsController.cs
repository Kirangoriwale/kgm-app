using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class UserRightsController : Controller
{
    private readonly AppDbContext _db;
    private static readonly string[] AllowedRoles = ["Admin", "Treasurer", "Secretary", "Committee", "Member"];

    public UserRightsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? role)
    {
        var selected = NormalizeRole(role);
        var existing = await _db.RoleMenuPermissions
            .AsNoTracking()
            .Where(r => r.RoleName == selected)
            .ToListAsync();

        var byKey = existing.ToDictionary(x => x.MenuKey, StringComparer.Ordinal);

        var vm = new UserRightsIndexViewModel { SelectedRole = selected };
        foreach (var menu in MenuCatalog.All)
        {
            if (byKey.TryGetValue(menu.Key, out var row))
            {
                vm.Items.Add(new MenuPermissionEditItem
                {
                    MenuKey = menu.Key,
                    CanView = row.CanView,
                    CanAdd = row.CanAdd,
                    CanEdit = row.CanEdit,
                    CanDelete = row.CanDelete
                });
            }
            else
            {
                vm.Items.Add(new MenuPermissionEditItem { MenuKey = menu.Key });
            }
        }

        ViewBag.RoleList = AllowedRoles
            .Select(r => new SelectListItem(r, r, string.Equals(r, selected, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string role, UserRightsIndexViewModel model)
    {
        var selected = NormalizeRole(role);
        if (model.Items == null || model.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "No permission rows were submitted.";
            return RedirectToAction(nameof(Index), new { role = selected });
        }

        var existing = await _db.RoleMenuPermissions.Where(r => r.RoleName == selected).ToListAsync();
        _db.RoleMenuPermissions.RemoveRange(existing);

        foreach (var item in model.Items)
        {
            if (string.IsNullOrWhiteSpace(item.MenuKey))
                continue;
            _db.RoleMenuPermissions.Add(new RoleMenuPermission
            {
                RoleName = selected,
                MenuKey = item.MenuKey.Trim(),
                CanView = item.CanView,
                CanAdd = item.CanAdd,
                CanEdit = item.CanEdit,
                CanDelete = item.CanDelete
            });
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"User rights saved for role '{selected}'.";
        return RedirectToAction(nameof(Index), new { role = selected });
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Admin";
        var match = AllowedRoles.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? "Admin";
    }
}
