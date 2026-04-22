using KgmApp.Data;
using KgmApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    public const string SessionKeyUsername = "Username";
    public const string SessionKeyRole = "Role";
    public const string SessionKeyMemberId = "MemberId";
    public const string SessionKeyMemberName = "MemberName";
    public const string SessionKeyMustChangePassword = "MustChangePassword";

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
        {
            if (string.Equals(HttpContext.Session.GetString(SessionKeyMustChangePassword), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(ChangePassword));
            return RedirectToHomeForRole(HttpContext.Session.GetString(SessionKeyRole));
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.MobileNo = model.MobileNo?.Trim() ?? string.Empty;
        TryValidateModel(model);
        if (!ModelState.IsValid)
            return View(model);

        var mobile = model.MobileNo;
        var member = await _db.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MobileNo == mobile);

        if (member == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid mobile number or password.");
            return View(model);
        }

        if (!member.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Login is not allowed because this member account is inactive.");
            return View(model);
        }

        if (member.RestrictLogin)
        {
            ModelState.AddModelError(string.Empty, "Login is not allowed because login is restricted for this account.");
            return View(model);
        }

        if (string.IsNullOrEmpty(member.LoginPassword) || member.LoginPassword != model.Password)
        {
            ModelState.AddModelError(string.Empty, "Invalid mobile number or password.");
            return View(model);
        }

        HttpContext.Session.SetString(SessionKeyUsername, member.MobileNo);
        HttpContext.Session.SetString(SessionKeyRole, member.Role);
        HttpContext.Session.SetString(SessionKeyMemberId, member.Id.ToString());
        HttpContext.Session.SetString(SessionKeyMemberName, member.Name);

        _db.LoginLogs.Add(new LoginLog
        {
            UserName = member.Name,
            MobileNo = member.MobileNo,
            LoginTimeUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        if (member.IsFirstLogin)
            HttpContext.Session.SetString(SessionKeyMustChangePassword, "true");
        else
            HttpContext.Session.Remove(SessionKeyMustChangePassword);

        if (member.IsFirstLogin)
            return RedirectToAction(nameof(ChangePassword));

        return RedirectToHomeForRole(member.Role);
    }

    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyMustChangePassword)))
            return RedirectToHomeForRole(HttpContext.Session.GetString(SessionKeyRole));

        if (!await MemberAllowsLoginAsync())
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> SettingsChangePassword()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        if (!await MemberAllowsLoginAsync())
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        ViewData["Title"] = "Change Password";
        ViewData["PageTitle"] = "Change Password";
        ViewData["BreadcrumbCurrent"] = "Change Password";
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        var memberIdStr = HttpContext.Session.GetString(SessionKeyMemberId);
        if (!int.TryParse(memberIdStr, out var memberId))
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        if (!member.IsActive || member.RestrictLogin)
        {
            HttpContext.Session.Clear();
            TempData["ErrorMessage"] = "Your session is no longer valid. Please contact an administrator.";
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
            return View(model);

        if (member.LoginPassword != model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        member.LoginPassword = model.NewPassword;
        member.IsFirstLogin = false;
        await _db.SaveChangesAsync();

        HttpContext.Session.Remove(SessionKeyMustChangePassword);

        TempData["SuccessMessage"] = "Your password has been updated.";
        return RedirectToHomeForRole(member.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettingsChangePassword(ChangePasswordViewModel model)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        var memberIdStr = HttpContext.Session.GetString(SessionKeyMemberId);
        if (!int.TryParse(memberIdStr, out var memberId))
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId);
        if (member == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        if (!member.IsActive || member.RestrictLogin)
        {
            HttpContext.Session.Clear();
            TempData["ErrorMessage"] = "Your session is no longer valid. Please contact an administrator.";
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Change Password";
            ViewData["PageTitle"] = "Change Password";
            ViewData["BreadcrumbCurrent"] = "Change Password";
            return View(nameof(SettingsChangePassword), model);
        }

        if (member.LoginPassword != model.CurrentPassword)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            ViewData["Title"] = "Change Password";
            ViewData["PageTitle"] = "Change Password";
            ViewData["BreadcrumbCurrent"] = "Change Password";
            return View(nameof(SettingsChangePassword), model);
        }

        member.LoginPassword = model.NewPassword;
        member.IsFirstLogin = false;
        await _db.SaveChangesAsync();

        HttpContext.Session.Remove(SessionKeyMustChangePassword);

        TempData["SuccessMessage"] = "Your password has been updated.";
        return RedirectToAction(nameof(SettingsChangePassword));
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToHomeForRole(string? role)
    {
        if (string.Equals(role, "Member", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("MyDashboard", "Home");
        return RedirectToAction("Index", "Home");
    }

    private async Task<bool> MemberAllowsLoginAsync()
    {
        var idStr = HttpContext.Session.GetString(SessionKeyMemberId);
        if (!int.TryParse(idStr, out var id))
            return false;

        var m = await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return m != null && m.IsActive && !m.RestrictLogin;
    }
}
