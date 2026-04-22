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

    public async Task<IActionResult> Index()
    {
        var role = HttpContext.Session.GetString(AccountController.SessionKeyRole);
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var rows = await _db.LoginLogs
            .AsNoTracking()
            .OrderByDescending(x => x.LoginTimeUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        ViewData["Title"] = "Login Logs";
        ViewData["PageTitle"] = "Login Logs";
        ViewData["BreadcrumbCurrent"] = "Login Logs";
        return View(rows);
    }
}
