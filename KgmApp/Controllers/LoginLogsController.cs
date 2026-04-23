using KgmApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public sealed class LoginLogsController : Controller
{
    private readonly AppDbContext _db;

    public LoginLogsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        search = search?.Trim();

        var role = HttpContext.Session.GetString(AccountController.SessionKeyRole);
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var query = _db.LoginLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(search)) ||
                (x.MobileNo != null && x.MobileNo.Contains(search)));
        }

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        if (page > totalPages)
            page = totalPages;

        var rows = await query
            .OrderByDescending(x => x.LoginTimeUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewData["Title"] = "Login Logs";
        ViewData["PageTitle"] = "Login Logs";
        ViewData["BreadcrumbCurrent"] = "Login Logs";
        ViewData["Search"] = search;
        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = totalPages;
        ViewData["TotalItems"] = totalItems;
        return View(rows);
    }
}
