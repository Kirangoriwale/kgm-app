using System.Collections.Generic;
using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class SubMembersController : Controller
{
    private readonly AppDbContext _db;

    public SubMembersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var list = await _db.SubMembers
                .AsNoTracking()
                .Include(s => s.Member)
                .OrderBy(s => s.Member!.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();
            return View(list);
        }
        catch
        {
            return View(new List<SubMember>());
        }
    }

    public IActionResult Create()
    {
        return View();
    }
}
