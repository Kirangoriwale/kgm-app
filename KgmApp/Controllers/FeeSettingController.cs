using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class FeeSettingController : Controller
{
    private readonly AppDbContext _db;

    public FeeSettingController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var rows = await _db.FeeSettings
            .AsNoTracking()
            .OrderByDescending(x => x.ApplyFromDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return View(rows);
    }

    public IActionResult Create()
    {
        return View(new FeeSetting { ApplyFromDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ApplyFromDate,ContributionFee,RegistrationFee")] FeeSetting feeSetting)
    {
        feeSetting.ApplyFromDate = DateTime.SpecifyKind(feeSetting.ApplyFromDate.Date, DateTimeKind.Utc);

        if (!ModelState.IsValid)
            return View(feeSetting);

        if (await _db.FeeSettings.AnyAsync(x => x.ApplyFromDate == feeSetting.ApplyFromDate))
        {
            ModelState.AddModelError(nameof(FeeSetting.ApplyFromDate), "A fee setting already exists for this Apply From Date.");
            return View(feeSetting);
        }

        _db.FeeSettings.Add(feeSetting);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var row = await _db.FeeSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (row == null)
            return NotFound();

        return View(row);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,ApplyFromDate,ContributionFee,RegistrationFee")] FeeSetting feeSetting)
    {
        if (id != feeSetting.Id)
            return NotFound();

        feeSetting.ApplyFromDate = DateTime.SpecifyKind(feeSetting.ApplyFromDate.Date, DateTimeKind.Utc);

        if (!ModelState.IsValid)
            return View(feeSetting);

        if (await _db.FeeSettings.AnyAsync(x => x.ApplyFromDate == feeSetting.ApplyFromDate && x.Id != feeSetting.Id))
        {
            ModelState.AddModelError(nameof(FeeSetting.ApplyFromDate), "A fee setting already exists for this Apply From Date.");
            return View(feeSetting);
        }

        try
        {
            _db.FeeSettings.Update(feeSetting);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.FeeSettings.AnyAsync(x => x.Id == feeSetting.Id))
                return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var row = await _db.FeeSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (row == null)
            return NotFound();

        return View(row);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var row = await _db.FeeSettings.FindAsync(id);
        if (row != null)
        {
            _db.FeeSettings.Remove(row);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}

