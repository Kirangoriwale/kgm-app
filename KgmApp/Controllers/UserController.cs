using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class UserController : Controller
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync();

        return View(users);
    }

    public IActionResult Create()
    {
        return View(new User { IsActive = true, Role = "User" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Username,Password,FullName,IsActive,Role")] User user)
    {
        if (!ModelState.IsValid)
            return View(user);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Username,Password,FullName,IsActive,Role")] User user)
    {
        if (id != user.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(user);

        try
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UserExists(user.Id))
                return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
            return NotFound();

        return View(user);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private Task<bool> UserExists(int id) =>
        _db.Users.AnyAsync(u => u.Id == id);
}
