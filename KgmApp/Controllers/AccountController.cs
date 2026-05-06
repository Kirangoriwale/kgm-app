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
    public async Task<IActionResult> Login()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
        {
            if (string.Equals(HttpContext.Session.GetString(SessionKeyMustChangePassword), "true", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(ChangePassword));

            var currentMember = await GetCurrentSessionMemberAsync();
            if (currentMember == null)
            {
                HttpContext.Session.Clear();
                return View(new LoginViewModel());
            }

            return RedirectToPostLoginDestination(currentMember);
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

        return RedirectToPostLoginDestination(member);
    }

    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyMustChangePassword)))
        {
            var member = await GetCurrentSessionMemberAsync();
            if (member == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction(nameof(Login));
            }

            return RedirectToPostLoginDestination(member);
        }

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
        return RedirectToPostLoginDestination(member);
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
    public async Task<IActionResult> RegistrationForm()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        if (string.Equals(HttpContext.Session.GetString(SessionKeyMustChangePassword), "true", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(ChangePassword));

        var member = await GetCurrentSessionMemberAsync();
        if (member == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        if (member.IsRegistrationFormSubmitted)
            return RedirectToPostLoginDestination(member);

        var vm = new MemberRegistrationFormViewModel
        {
            Name = member.Name,
            MobileNo = member.MobileNo,
            Terms = member.Terms,
            EmailId = member.EmailId,
            Address = member.Address,
            DateOfBirth = member.DateOfBirth,
            AadhaarNo = member.AadhaarNo,
            Education = member.Education,
            BusinessOrJob = member.BusinessOrJob,
            TermsAcceptYN = member.TermsAcceptYN ?? false
        };
        ViewData["RegistrationTermsHtml"] = await GetRegistrationTermsHtmlAsync(member);

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrationForm([Bind("EmailId,Address,DateOfBirth,AadhaarNo,Education,BusinessOrJob,TermsAcceptYN")] MemberRegistrationFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeyUsername)))
            return RedirectToAction(nameof(Login));

        if (string.Equals(HttpContext.Session.GetString(SessionKeyMustChangePassword), "true", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(ChangePassword));

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

        model.Name = member.Name;
        model.MobileNo = member.MobileNo;
        model.Terms = member.Terms;
        model.EmailId = model.EmailId?.Trim();
        model.Address = model.Address?.Trim();
        model.AadhaarNo = model.AadhaarNo?.Trim();
        model.Education = model.Education?.Trim();
        model.BusinessOrJob = model.BusinessOrJob?.Trim();
        model.DateOfBirth = NormalizeToUtc(model.DateOfBirth);
        TryValidateModel(model);

        var termsHtml = await GetRegistrationTermsHtmlAsync(member);
        if (string.IsNullOrWhiteSpace(termsHtml))
            ModelState.AddModelError(nameof(model.Terms), "Terms are not available. Please contact administrator.");

        if (!ModelState.IsValid)
        {
            ViewData["RegistrationTermsHtml"] = termsHtml;
            return View(model);
        }

        member.EmailId = model.EmailId ?? string.Empty;
        member.Address = model.Address ?? string.Empty;
        member.DateOfBirth = model.DateOfBirth;
        member.AadhaarNo = model.AadhaarNo;
        member.Education = model.Education;
        member.BusinessOrJob = model.BusinessOrJob;
        member.Terms = termsHtml?.Trim();
        member.TermsAcceptYN = model.TermsAcceptYN;
        member.IsRegistrationFormSubmitted = true;

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Registration form submitted successfully.";
        return RedirectToPostLoginDestination(member);
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToPostLoginDestination(Member member)
    {
        if (!member.IsRegistrationFormSubmitted)
            return RedirectToAction(nameof(RegistrationForm));

        if (string.Equals(member.Role, "Member", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("MyDashboard", "Home");
        return RedirectToAction("Index", "Home");
    }

    private async Task<Member?> GetCurrentSessionMemberAsync()
    {
        var idStr = HttpContext.Session.GetString(SessionKeyMemberId);
        if (!int.TryParse(idStr, out var id))
            return null;

        return await _db.Members.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    private async Task<string?> GetRegistrationTermsHtmlAsync(Member member)
    {
        var rulesHtml = await _db.RulesRegulations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => x.ContentHtml)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(rulesHtml))
            return rulesHtml;

        if (!string.IsNullOrWhiteSpace(member.Terms))
            return member.Terms;

        return null;
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var dt = value.Value;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
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
