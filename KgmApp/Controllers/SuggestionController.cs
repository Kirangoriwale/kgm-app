using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public sealed class SuggestionController : Controller
{
    private readonly AppDbContext _db;

    public SuggestionController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var role = HttpContext.Session.GetString(AccountController.SessionKeyRole) ?? string.Empty;
        var memberIdStr = HttpContext.Session.GetString(AccountController.SessionKeyMemberId);
        if (!int.TryParse(memberIdStr, out var memberId))
            return RedirectToAction("Login", "Account");

        var query = _db.Suggestions
            .AsNoTracking()
            .AsQueryable();

        // Members can only see their own suggestions; other roles can see all.
        if (string.Equals(role, "Member", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.MemberId == memberId);

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        ViewData["Title"] = "Suggestions";
        ViewData["PageTitle"] = "Suggestions";
        ViewData["BreadcrumbCurrent"] = "Suggestions";
        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Content")] Suggestion model)
    {
        var memberIdStr = HttpContext.Session.GetString(AccountController.SessionKeyMemberId);
        if (!int.TryParse(memberIdStr, out var memberId))
            return RedirectToAction("Login", "Account");

        model.Title = (model.Title ?? string.Empty).Trim();
        model.Content = (model.Content ?? string.Empty).Trim();
        if (!ModelState.IsValid)
            return await Index();

        var memberName = HttpContext.Session.GetString(AccountController.SessionKeyMemberName)
            ?? HttpContext.Session.GetString(AccountController.SessionKeyUsername)
            ?? "Unknown";

        var row = new Suggestion
        {
            MemberId = memberId,
            MemberName = memberName,
            Title = model.Title,
            Content = model.Content,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Suggestions.Add(row);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Suggestion submitted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (!IsAdmin())
            return Forbid();

        if (id == null)
            return NotFound();

        var row = await _db.Suggestions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);
        if (row == null)
            return NotFound();

        ViewData["Title"] = "Delete Suggestion";
        ViewData["PageTitle"] = "Delete Suggestion";
        ViewData["BreadcrumbCurrent"] = "Delete Suggestion";
        return View(row);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!IsAdmin())
            return Forbid();

        var row = await _db.Suggestions.FirstOrDefaultAsync(x => x.Id == id);
        if (row != null)
        {
            _db.Suggestions.Remove(row);
            await _db.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = "Suggestion deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private bool IsAdmin() =>
        string.Equals(HttpContext.Session.GetString(AccountController.SessionKeyRole), "Admin", StringComparison.OrdinalIgnoreCase);
}
