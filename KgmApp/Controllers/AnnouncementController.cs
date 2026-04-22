using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public sealed class AnnouncementController : Controller
{
    private readonly AppDbContext _db;
    private const int DefaultPageSize = 10;

    public AnnouncementController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (page < 1)
            page = 1;

        var totalCount = await _db.Announcements.AsNoTracking().CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)DefaultPageSize));
        if (page > totalPages)
            page = totalPages;

        var rows = await _db.Announcements
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        var vm = new AnnouncementIndexViewModel
        {
            Items = rows,
            CurrentPage = page,
            TotalPages = totalPages,
            PageSize = DefaultPageSize
        };

        ViewData["Title"] = "Announcements";
        ViewData["PageTitle"] = "Announcements";
        ViewData["BreadcrumbCurrent"] = "Announcements";
        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Announcement";
        ViewData["PageTitle"] = "Create Announcement";
        ViewData["BreadcrumbCurrent"] = "Create Announcement";
        return View(new Announcement());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,ContentHtml")] Announcement model)
    {
        model.Title = (model.Title ?? string.Empty).Trim();
        var html = (model.ContentHtml ?? string.Empty).Trim();
        if (string.Equals(html, "<p><br></p>", StringComparison.OrdinalIgnoreCase))
            html = string.Empty;
        model.ContentHtml = html;

        if (string.IsNullOrWhiteSpace(model.ContentHtml))
            ModelState.AddModelError(nameof(Announcement.ContentHtml), "Announcement content is required.");

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create Announcement";
            ViewData["PageTitle"] = "Create Announcement";
            ViewData["BreadcrumbCurrent"] = "Create Announcement";
            return View(model);
        }

        var author = HttpContext.Session.GetString(AccountController.SessionKeyMemberName)
                     ?? HttpContext.Session.GetString(AccountController.SessionKeyUsername)
                     ?? "Unknown";

        model.CreatedBy = author;
        model.CreatedAtUtc = DateTime.UtcNow;

        _db.Announcements.Add(model);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Announcement created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (!IsAdmin())
            return Forbid();

        if (id == null)
            return NotFound();

        var row = await _db.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);
        if (row == null)
            return NotFound();

        ViewData["Title"] = "Delete Announcement";
        ViewData["PageTitle"] = "Delete Announcement";
        ViewData["BreadcrumbCurrent"] = "Delete Announcement";
        return View(row);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!IsAdmin())
            return Forbid();

        var row = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
        if (row != null)
        {
            _db.Announcements.Remove(row);
            await _db.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = "Announcement deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsAdmin() =>
        string.Equals(HttpContext.Session.GetString(AccountController.SessionKeyRole), "Admin", StringComparison.OrdinalIgnoreCase);
}
