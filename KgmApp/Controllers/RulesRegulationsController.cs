using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public sealed class RulesRegulationsController : Controller
{
    private readonly AppDbContext _db;

    public RulesRegulationsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var row = await _db.RulesRegulations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        row ??= new RulesRegulation();

        ViewData["Title"] = "Rules & Regulations";
        ViewData["PageTitle"] = "Rules & Regulations";
        ViewData["BreadcrumbCurrent"] = "Rules & Regulations";

        return View(row);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind("Id,ContentHtml")] RulesRegulation model)
    {
        var html = (model.ContentHtml ?? string.Empty).Trim();
        if (string.Equals(html, "<p><br></p>", StringComparison.OrdinalIgnoreCase))
            html = string.Empty;

        if (string.IsNullOrWhiteSpace(html))
        {
            ModelState.AddModelError(nameof(RulesRegulation.ContentHtml), "Rules and regulations content is required.");
            ViewData["Title"] = "Rules & Regulations";
            ViewData["PageTitle"] = "Rules & Regulations";
            ViewData["BreadcrumbCurrent"] = "Rules & Regulations";
            return View("Index", model);
        }

        var row = await _db.RulesRegulations
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        if (row == null)
        {
            row = new RulesRegulation
            {
                ContentHtml = html,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.RulesRegulations.Add(row);
        }
        else
        {
            row.ContentHtml = html;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Rules and regulations updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
