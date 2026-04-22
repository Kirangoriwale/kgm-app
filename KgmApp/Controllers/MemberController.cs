using KgmApp.Data;
using KgmApp.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace KgmApp.Controllers;

public class MemberController : Controller
{
    private readonly AppDbContext _db;
    private static readonly string[] AllowedRoles = ["Admin", "Treasurer", "Secretary", "Committee", "Member"];

    public MemberController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var members = await _db.Members
                .AsNoTracking()
                .OrderBy(m => m.Sr)
                .ThenBy(m => m.Name)
                .ToListAsync();
            return View(members);
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            ViewData["DatabaseUnavailable"] = true;
            return View(Enumerable.Empty<Member>());
        }
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var member = await _db.Members
            .AsNoTracking()
            .Include(m => m.SubMembers)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (member == null)
            return NotFound();

        return View(member);
    }

    public IActionResult Create()
    {
        PopulateRoleOptions();
        return View(new Member { IsActive = true, Sr = 0, Role = "Member" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Sr,Name,MobileNo,EmailId,Address,IsActive,IsRegistrationFormSubmitted,IsCommiteeMember,RestrictLogin,LoginPassword,IsFirstLogin,Role,Designation")] Member member)
    {
        member.MobileNo = member.MobileNo?.Trim() ?? string.Empty;
        TryValidateModel(member);

        if (!IsValidRole(member.Role))
            ModelState.AddModelError(nameof(member.Role), "Please select a valid role.");

        if (ModelState.IsValid && await MobileNoExistsAsync(member.MobileNo, excludeMemberId: null))
            ModelState.AddModelError(nameof(member.MobileNo), "This mobile number is already registered.");

        if (ModelState.IsValid)
        {
            try
            {
                _db.Add(member);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
            {
                ModelState.AddModelError(string.Empty, "Database is currently unavailable. Please check the connection and try again.");
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                ModelState.AddModelError(nameof(member.MobileNo), "This mobile number is already registered.");
            }
        }

        PopulateRoleOptions();
        return View(member);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (member == null)
            return NotFound();

        PopulateRoleOptions();
        await PopulateEditSubMemberData(member.Id);
        return View(member);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Sr,Name,MobileNo,EmailId,Address,IsActive,IsRegistrationFormSubmitted,IsCommiteeMember,RestrictLogin,LoginPassword,IsFirstLogin,Role,Designation")] Member member)
    {
        if (id != member.Id)
            return NotFound();

        member.MobileNo = member.MobileNo?.Trim() ?? string.Empty;
        TryValidateModel(member);

        if (!IsValidRole(member.Role))
            ModelState.AddModelError(nameof(member.Role), "Please select a valid role.");

        if (ModelState.IsValid && await MobileNoExistsAsync(member.MobileNo, excludeMemberId: member.Id))
            ModelState.AddModelError(nameof(member.MobileNo), "This mobile number is already registered.");

        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(member);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
            {
                ModelState.AddModelError(string.Empty, "Database is currently unavailable. Please check the connection and try again.");
                PopulateRoleOptions();
                await PopulateEditSubMemberData(member.Id);
                return View(member);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                ModelState.AddModelError(nameof(member.MobileNo), "This mobile number is already registered.");
                PopulateRoleOptions();
                await PopulateEditSubMemberData(member.Id);
                return View(member);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await MemberExists(member.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        PopulateRoleOptions();
        await PopulateEditSubMemberData(member.Id);
        return View(member);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (member == null)
            return NotFound();

        return View(member);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            var member = await _db.Members.FindAsync(id);
            if (member != null)
            {
                _db.Members.Remove(member);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["ErrorMessage"] = "Database is currently unavailable. Delete could not be completed.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportExcel(IFormFile? excelFile)
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an Excel file to import.";
            return RedirectToAction(nameof(ImportExcel));
        }

        if (!string.Equals(Path.GetExtension(excelFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only .xlsx files are supported for import.";
            return RedirectToAction(nameof(ImportExcel));
        }

        try
        {
            using var stream = excelFile.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                TempData["ErrorMessage"] = "The uploaded Excel file does not contain any worksheet.";
                return RedirectToAction(nameof(ImportExcel));
            }

            if (!TryReadHeaderMap(worksheet, out var headers))
            {
                TempData["ErrorMessage"] = "Invalid header row. Required columns: Sr, Name, MobileNo, Address.";
                return RedirectToAction(nameof(ImportExcel));
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null || usedRange.RowCount() <= 1)
            {
                TempData["ErrorMessage"] = "No member rows found in Excel file.";
                return RedirectToAction(nameof(ImportExcel));
            }

            // Import creates new members only; existing Sr or MobileNo in the database is rejected.
            var members = await _db.Members.ToListAsync();

            var inserted = 0;
            var skipped = 0;
            var rowErrors = new List<string>();
            var mobilesSeenInFile = new HashSet<string>(StringComparer.Ordinal);
            var srSeenInFile = new HashSet<int>();

            var lastRow = usedRange.LastRow().RowNumber();
            for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
            {
                var row = worksheet.Row(rowIndex);

                var srRaw = GetCellValue(row, headers, "sr");
                var name = GetCellValue(row, headers, "name");
                var mobile = GetCellValue(row, headers, "mobileno");
                var address = GetCellValue(row, headers, "address");

                var isCompletelyBlank = string.IsNullOrWhiteSpace(srRaw)
                    && string.IsNullOrWhiteSpace(name)
                    && string.IsNullOrWhiteSpace(mobile)
                    && string.IsNullOrWhiteSpace(address);

                if (isCompletelyBlank)
                    continue;

                if (!int.TryParse(srRaw, out var sr) || sr <= 0)
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: invalid Sr.");
                    continue;
                }

                if (!srSeenInFile.Add(sr))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: duplicate Sr in file (import creates new members only).");
                    continue;
                }

                if (members.Any(m => m.Sr == sr))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: Sr {sr} already exists. Use Edit Member to change existing records.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(address))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: Name, MobileNo and Address are required.");
                    continue;
                }

                if (!IsTenDigitMobile(mobile))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: MobileNo must be exactly 10 digits.");
                    continue;
                }

                if (!mobilesSeenInFile.Add(mobile))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: duplicate MobileNo in file.");
                    continue;
                }

                var email = GetCellValue(row, headers, "emailid");
                var designation = GetCellValue(row, headers, "designation");
                var isActiveRaw = GetCellValue(row, headers, "isactive");
                if (string.IsNullOrWhiteSpace(isActiveRaw))
                    isActiveRaw = GetCellValue(row, headers, "active");
                var isActive = ParseBoolean(isActiveRaw, true);

                var isRegSubmittedRaw = GetCellValue(row, headers, "isregistrationformsubmitted");
                if (string.IsNullOrWhiteSpace(isRegSubmittedRaw))
                    isRegSubmittedRaw = GetCellValue(row, headers, "registrationformsubmitted");
                var isRegSubmitted = ParseBoolean(isRegSubmittedRaw, false);
                var isCommiteeMemberRaw = GetCellValue(row, headers, "iscommiteemember");
                if (string.IsNullOrWhiteSpace(isCommiteeMemberRaw))
                    isCommiteeMemberRaw = GetCellValue(row, headers, "commiteemember");
                var isCommiteeMember = ParseBoolean(isCommiteeMemberRaw, false);
                var restrictLoginRaw = GetCellValue(row, headers, "restrictlogin");
                var restrictLogin = ParseBoolean(restrictLoginRaw, false);
                var loginPassword = GetCellValue(row, headers, "loginpassword");

                var hasIsFirstLoginColumn = headers.ContainsKey("isfirstlogin") || headers.ContainsKey("firstlogin");
                var isFirstLoginRaw = headers.ContainsKey("isfirstlogin")
                    ? GetCellValue(row, headers, "isfirstlogin")
                    : GetCellValue(row, headers, "firstlogin");
                bool? isFirstLoginValue = null;
                if (hasIsFirstLoginColumn)
                    isFirstLoginValue = ParseBoolean(isFirstLoginRaw, defaultValue: true);

                var role = GetCellValue(row, headers, "role");
                if (string.IsNullOrWhiteSpace(role))
                    role = "Member";
                role = NormalizeRole(role);
                if (!IsValidRole(role))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: invalid Role '{role}'.");
                    continue;
                }

                if (members.Any(m => m.MobileNo == mobile))
                {
                    skipped++;
                    rowErrors.Add($"Row {rowIndex}: MobileNo is already registered.");
                    continue;
                }

                var newMember = new Member
                {
                    Sr = sr,
                    Name = name,
                    MobileNo = mobile,
                    EmailId = email,
                    Address = address,
                    IsActive = isActive,
                    IsRegistrationFormSubmitted = isRegSubmitted,
                    IsCommiteeMember = isCommiteeMember,
                    RestrictLogin = restrictLogin,
                    LoginPassword = loginPassword,
                    IsFirstLogin = isFirstLoginValue ?? true,
                    Role = role,
                    Designation = designation
                };
                _db.Members.Add(newMember);
                members.Add(newMember);
                inserted++;
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Import completed. New members inserted: {inserted}. Skipped rows: {skipped}.";
            if (rowErrors.Count > 0)
                TempData["ErrorMessage"] = string.Join(" ", rowErrors.Take(5)) + (rowErrors.Count > 5 ? " ..." : string.Empty);
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["ErrorMessage"] = "Database is currently unavailable. Import could not be completed.";
        }
        catch (Exception)
        {
            TempData["ErrorMessage"] = "Failed to import Excel file. Please verify the file format and try again.";
        }

        return RedirectToAction(nameof(ImportExcel));
    }

    [HttpGet]
    public IActionResult ImportExcel()
    {
        ViewData["PageTitle"] = "Import Members";
        ViewData["BreadcrumbCurrent"] = "Import Members";
        return View();
    }

    [HttpGet]
    public IActionResult DownloadImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Members");

        // All importable Member fields for new rows only (Id is database-generated; SubMembers are not in this sheet)
        ws.Cell(1, 1).Value = "Sr";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "MobileNo";
        ws.Cell(1, 4).Value = "EmailId";
        ws.Cell(1, 5).Value = "Address";
        ws.Cell(1, 6).Value = "Designation";
        ws.Cell(1, 7).Value = "Role";
        ws.Cell(1, 8).Value = "LoginPassword";
        ws.Cell(1, 9).Value = "IsFirstLogin";
        ws.Cell(1, 10).Value = "IsActive";
        ws.Cell(1, 11).Value = "IsRegistrationFormSubmitted";
        ws.Cell(1, 12).Value = "IsCommiteeMember";
        ws.Cell(1, 13).Value = "RestrictLogin";

        // Example row — Sr must not already exist in the database (create-only import)
        ws.Cell(2, 1).Value = 1;
        ws.Cell(2, 2).Value = "Sample Member";
        ws.Cell(2, 3).Value = "9876543210";
        ws.Cell(2, 4).Value = "sample@example.com";
        ws.Cell(2, 5).Value = "Sample Address";
        ws.Cell(2, 6).Value = "Member";
        ws.Cell(2, 7).Value = "Member";
        ws.Cell(2, 8).Value = "pass@123";
        ws.Cell(2, 9).Value = "Yes";
        ws.Cell(2, 10).Value = "Yes";
        ws.Cell(2, 11).Value = "No";
        ws.Cell(2, 12).Value = "No";
        ws.Cell(2, 13).Value = "No";

        var headerRange = ws.Range(1, 1, 1, 13);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#e9f2ff");

        ws.Columns(1, 13).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "MemberImportTemplate.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubMember([Bind("SrNo,MemberId,Name,MobileNo,EmailId,Relation,IsActive,IsRegistrationFormSubmitted,Designation")] SubMember subMember)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == subMember.MemberId);
        if (member == null)
            return NotFound();

        if (!ValidateSubMemberInput(subMember, out var validationError))
        {
            TempData["ErrorMessage"] = validationError;
            return RedirectToAction(nameof(Edit), new { id = subMember.MemberId });
        }

        try
        {
            _db.SubMembers.Add(subMember);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = subMember.MemberId });
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["ErrorMessage"] = "Database is currently unavailable. Please check the connection and try again.";
            return RedirectToAction(nameof(Edit), new { id = subMember.MemberId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubMember([Bind("Id,SrNo,MemberId,Name,MobileNo,EmailId,Relation,IsActive,IsRegistrationFormSubmitted,Designation")] SubMember subMember)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == subMember.MemberId);
        if (member == null)
            return NotFound();

        if (!ValidateSubMemberInput(subMember, out var validationError))
        {
            TempData["ErrorMessage"] = validationError;
            return RedirectToAction(nameof(Edit), new { id = subMember.MemberId });
        }

        var existing = await _db.SubMembers.FirstOrDefaultAsync(s => s.Id == subMember.Id && s.MemberId == subMember.MemberId);
        if (existing == null)
            return NotFound();

        try
        {
            existing.Name = subMember.Name;
            existing.SrNo = subMember.SrNo;
            existing.MobileNo = subMember.MobileNo;
            existing.EmailId = subMember.EmailId;
            existing.Relation = subMember.Relation;
            existing.IsActive = subMember.IsActive;
            existing.IsRegistrationFormSubmitted = subMember.IsRegistrationFormSubmitted;
            existing.Designation = subMember.Designation;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["ErrorMessage"] = "Database is currently unavailable. Edit could not be completed.";
        }

        return RedirectToAction(nameof(Edit), new { id = subMember.MemberId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubMember(int memberId, int subMemberId)
    {
        var existing = await _db.SubMembers.FirstOrDefaultAsync(s => s.Id == subMemberId && s.MemberId == memberId);
        if (existing == null)
            return NotFound();

        try
        {
            _db.SubMembers.Remove(existing);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["ErrorMessage"] = "Database is currently unavailable. Delete could not be completed.";
        }

        return RedirectToAction(nameof(Edit), new { id = memberId });
    }

    private Task<bool> MemberExists(int id) =>
        _db.Members.AnyAsync(e => e.Id == id);

    private Task<bool> MobileNoExistsAsync(string mobileNo, int? excludeMemberId)
    {
        var q = _db.Members.AsNoTracking().Where(m => m.MobileNo == mobileNo);
        if (excludeMemberId.HasValue)
            q = q.Where(m => m.Id != excludeMemberId.Value);
        return q.AnyAsync();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg && pg.SqlState == "23505")
                return true;
        }

        return false;
    }

    private static bool IsTenDigitMobile(string? value) =>
        value != null && Regex.IsMatch(value, @"^\d{10}$");

    private async Task PopulateEditSubMemberData(int memberId)
    {
        var subMembers = await _db.SubMembers
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderBy(s => s.SrNo)
            .ThenBy(s => s.Name)
            .ThenBy(s => s.MobileNo)
            .ToListAsync();

        ViewData["SubMembers"] = subMembers;
        ViewData["NewSubMember"] = new SubMember
        {
            MemberId = memberId,
            IsActive = true,
            IsRegistrationFormSubmitted = false
        };
    }

    private static bool IsDatabaseConnectionFailure(Exception ex)
    {
        if (ex is NpgsqlException || ex is SocketException)
            return true;

        var current = ex.InnerException;
        while (current != null)
        {
            if (current is NpgsqlException || current is SocketException)
                return true;
            current = current.InnerException;
        }

        return false;
    }

    private static bool ValidateSubMemberInput(SubMember subMember, out string errorMessage)
    {
        subMember.Name = subMember.Name?.Trim() ?? string.Empty;
        subMember.MobileNo = subMember.MobileNo?.Trim() ?? string.Empty;
        subMember.EmailId = subMember.EmailId?.Trim() ?? string.Empty;
        subMember.Relation = subMember.Relation?.Trim() ?? string.Empty;
        subMember.Designation = subMember.Designation?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(subMember.Name))
        {
            errorMessage = "Sub-member name is required.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(subMember.EmailId))
        {
            var emailValidator = new EmailAddressAttribute();
            if (!emailValidator.IsValid(subMember.EmailId))
            {
                errorMessage = "Please enter a valid email address.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadHeaderMap(IXLWorksheet worksheet, out Dictionary<string, int> headers)
    {
        headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(1);
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastCol == 0)
            return false;

        for (var col = 1; col <= lastCol; col++)
        {
            var raw = headerRow.Cell(col).GetValue<string>();
            var key = NormalizeHeader(raw);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            headers[key] = col;
        }

        return headers.ContainsKey("sr")
            && headers.ContainsKey("name")
            && headers.ContainsKey("mobileno")
            && headers.ContainsKey("address");
    }

    private static string GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headers, string headerKey)
    {
        if (!headers.TryGetValue(headerKey, out var col))
            return string.Empty;

        return row.Cell(col).GetValue<string>().Trim();
    }

    private static bool ParseBoolean(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1" or "y" or "yes" or "true" or "active" => true,
            "0" or "n" or "no" or "false" or "inactive" => false,
            _ => defaultValue
        };
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var chars = header.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private void PopulateRoleOptions()
    {
        ViewBag.MemberRoles = AllowedRoles
            .Select(role => new SelectListItem(role, role))
            .ToList();
    }

    private static bool IsValidRole(string? role)
    {
        return !string.IsNullOrWhiteSpace(role)
            && AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeRole(string role)
    {
        return AllowedRoles.FirstOrDefault(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase)) ?? role;
    }
}
