using KgmApp.Data;
using KgmApp.Models;
using KgmApp.Models.Meetings;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net.Sockets;
using Npgsql;

namespace KgmApp.Controllers;

public class MeetingController : Controller
{
    private readonly AppDbContext _db;
    private static readonly string[] AllowedMeetingTitles = ["General Meeting", "Committee Meeting"];

    public MeetingController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var totalMemberCount = await _db.Members.AsNoTracking().CountAsync();

        var meetings = await _db.Meetings
            .AsNoTracking()
            .OrderByDescending(m => m.MeetingDate)
            .ThenByDescending(m => m.Id)
            .Select(m => new MeetingListRow
            {
                Id = m.Id,
                MeetingDate = m.MeetingDate,
                Title = m.Title,
                Description = m.Description,
                Location = m.Location,
                MinutesOfMeeting = m.MinutesOfMeeting,
                PresentCount = _db.Attendances.Count(a => a.MeetingId == m.Id && a.IsPresent),
                TotalMarked = _db.Attendances.Count(a => a.MeetingId == m.Id)
            })
            .ToListAsync();

        ViewData["TotalMemberCount"] = totalMemberCount;
        return View(meetings);
    }

    public IActionResult Create()
    {
        PopulateMeetingTitleOptions();
        return View(new Meeting { MeetingDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("MeetingDate,Title,Description,Location,MinutesOfMeeting")] Meeting meeting)
    {
        meeting.MeetingDate = DateTime.SpecifyKind(meeting.MeetingDate.Date, DateTimeKind.Utc);
        meeting.Title = meeting.Title?.Trim() ?? string.Empty;
        meeting.Description = meeting.Description?.Trim() ?? string.Empty;
        meeting.Location = meeting.Location?.Trim() ?? string.Empty;
        meeting.MinutesOfMeeting = meeting.MinutesOfMeeting?.Trim() ?? string.Empty;

        if (!IsValidMeetingTitle(meeting.Title))
            ModelState.AddModelError(nameof(meeting.Title), "Please select a valid meeting title.");

        if (!ModelState.IsValid)
        {
            PopulateMeetingTitleOptions();
            return View(meeting);
        }

        meeting.CreatedAtUtc = DateTime.UtcNow;
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(MarkAttendance), new { id = meeting.Id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (meeting == null)
            return NotFound();

        PopulateMeetingTitleOptions();
        return View(meeting);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,MeetingDate,Title,Description,Location,MinutesOfMeeting")] Meeting meeting)
    {
        if (id != meeting.Id)
            return NotFound();

        meeting.MeetingDate = DateTime.SpecifyKind(meeting.MeetingDate.Date, DateTimeKind.Utc);
        meeting.Title = meeting.Title?.Trim() ?? string.Empty;
        meeting.Description = meeting.Description?.Trim() ?? string.Empty;
        meeting.Location = meeting.Location?.Trim() ?? string.Empty;
        meeting.MinutesOfMeeting = meeting.MinutesOfMeeting?.Trim() ?? string.Empty;

        if (!IsValidMeetingTitle(meeting.Title))
            ModelState.AddModelError(nameof(meeting.Title), "Please select a valid meeting title.");

        if (!ModelState.IsValid)
        {
            PopulateMeetingTitleOptions();
            return View(meeting);
        }

        var existing = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == id);
        if (existing == null)
            return NotFound();

        existing.MeetingDate = meeting.MeetingDate;
        existing.Title = meeting.Title;
        existing.Description = meeting.Description;
        existing.Location = meeting.Location;
        existing.MinutesOfMeeting = meeting.MinutesOfMeeting;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var meeting = await _db.Meetings
            .AsNoTracking()
            .Include(m => m.Attendances)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (meeting == null)
            return NotFound();

        return View(meeting);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(x => x.Id == id);
        if (meeting != null)
        {
            _db.Meetings.Remove(meeting);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> MarkAttendance(int? id)
    {
        if (id == null)
            return NotFound();

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (meeting == null)
            return NotFound();

        var membersQuery = _db.Members
            .AsNoTracking()
            .Where(m => m.IsActive)
            .AsQueryable();

        if (string.Equals(meeting.Title, "Committee Meeting", StringComparison.OrdinalIgnoreCase))
            membersQuery = membersQuery.Where(m => m.IsCommiteeMember);

        var members = await membersQuery
            .OrderBy(m => m.Sr)
            .ThenBy(m => m.Name)
            .Select(m => new { m.Id, m.Sr, m.Name, m.MobileNo })
            .ToListAsync();

        var attendanceLookup = await _db.Attendances
            .AsNoTracking()
            .Where(a => a.MeetingId == meeting.Id)
            .ToDictionaryAsync(a => a.MemberId, a => new { a.Id, a.IsPresent });

        var vm = new MeetingAttendanceViewModel
        {
            MeetingId = meeting.Id,
            MeetingTitle = meeting.Title,
            MeetingDate = meeting.MeetingDate,
            MeetingLocation = meeting.Location,
            MinutesOfMeeting = meeting.MinutesOfMeeting,
            Members = members.Select(m => new MemberAttendanceRow
            {
                MemberId = m.Id,
                Sr = m.Sr,
                MemberName = m.Name,
                MobileNo = m.MobileNo,
                AttendanceId = attendanceLookup.TryGetValue(m.Id, out var attendance) ? attendance.Id : null,
                IsPresent = attendanceLookup.TryGetValue(m.Id, out var present) && present.IsPresent
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAttendance(MeetingAttendanceSubmitModel model)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == model.MeetingId);
        if (meeting == null)
            return NotFound();

        var eligibleMembersQuery = _db.Members
            .AsNoTracking()
            .Where(m => m.IsActive)
            .AsQueryable();

        if (string.Equals(meeting.Title, "Committee Meeting", StringComparison.OrdinalIgnoreCase))
            eligibleMembersQuery = eligibleMembersQuery.Where(m => m.IsCommiteeMember);

        var activeMemberIds = await eligibleMembersQuery
            .Select(m => m.Id)
            .ToListAsync();
        var allowed = activeMemberIds.ToHashSet();

        var existing = await _db.Attendances
            .Where(a => a.MeetingId == model.MeetingId)
            .ToListAsync();

        var existingByMember = existing.ToDictionary(a => a.MemberId, a => a);
        var now = DateTime.UtcNow;

        foreach (var row in model.Members)
        {
            if (!allowed.Contains(row.MemberId))
                continue;

            if (existingByMember.TryGetValue(row.MemberId, out var attendance))
            {
                attendance.IsPresent = row.IsPresent;
                attendance.MarkedAtUtc = now;
            }
            else
            {
                _db.Attendances.Add(new Attendance
                {
                    MeetingId = model.MeetingId,
                    MemberId = row.MemberId,
                    IsPresent = row.IsPresent,
                    MarkedAtUtc = now
                });
            }
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "Attendance saved successfully.";
        return RedirectToAction(nameof(MarkAttendance), new { id = model.MeetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttendance(int meetingId, int attendanceId)
    {
        var attendance = await _db.Attendances
            .FirstOrDefaultAsync(a => a.Id == attendanceId && a.MeetingId == meetingId);
        if (attendance != null)
        {
            _db.Attendances.Remove(attendance);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Attendance mark deleted.";
        }

        return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportAttendanceExcel(int meetingId, IFormFile? excelFile)
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            TempData["LayoutError"] = "Please select an Excel file to import.";
            return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
        }

        if (!string.Equals(Path.GetExtension(excelFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["LayoutError"] = "Only .xlsx files are supported for import.";
            return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
        }

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound();

        try
        {
            using var stream = excelFile.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                TempData["LayoutError"] = "The uploaded Excel file does not contain any worksheet.";
                return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
            }

            if (!TryReadAttendanceHeaderMap(worksheet, out var headers))
            {
                TempData["LayoutError"] = "Invalid header row. Required columns: MemberId, Present.";
                return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
            }

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null || usedRange.RowCount() <= 1)
            {
                TempData["LayoutError"] = "No attendance rows found in Excel file.";
                return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
            }

            // Eligible members match the same rules as the MarkAttendance screen.
            var eligibleMembersQuery = _db.Members
                .AsNoTracking()
                .Where(m => m.IsActive)
                .AsQueryable();

            if (string.Equals(meeting.Title, "Committee Meeting", StringComparison.OrdinalIgnoreCase))
                eligibleMembersQuery = eligibleMembersQuery.Where(m => m.IsCommiteeMember);

            var eligibleMemberIds = await eligibleMembersQuery.Select(m => m.Id).ToListAsync();
            var eligible = eligibleMemberIds.ToHashSet();

            var updates = new Dictionary<int, bool>();
            var rowErrors = new List<string>();
            var lastRow = usedRange.LastRow().RowNumber();

            for (var rowIndex = 2; rowIndex <= lastRow; rowIndex++)
            {
                var row = worksheet.Row(rowIndex);
                var memberIdRaw = GetCellValue(row, headers, "memberid");
                var presentRaw = GetCellValue(row, headers, "present");

                var isCompletelyBlank = string.IsNullOrWhiteSpace(memberIdRaw) && string.IsNullOrWhiteSpace(presentRaw);
                if (isCompletelyBlank)
                    continue;

                if (!int.TryParse(memberIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memberId) || memberId <= 0)
                {
                    rowErrors.Add($"Row {rowIndex}: invalid MemberId.");
                    continue;
                }

                if (!eligible.Contains(memberId))
                {
                    rowErrors.Add($"Row {rowIndex}: MemberId {memberId} is not eligible for this meeting.");
                    continue;
                }

                if (!TryParseBoolean(presentRaw, out var isPresent))
                {
                    rowErrors.Add($"Row {rowIndex}: invalid Present value (use True/False).");
                    continue;
                }

                // If the file contains the same MemberId multiple times, last one wins.
                updates[memberId] = isPresent;
            }

            if (updates.Count == 0)
            {
                TempData["LayoutError"] = "No valid attendance rows found to import.";
                return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
            }

            var now = DateTime.UtcNow;
            var existing = await _db.Attendances
                .Where(a => a.MeetingId == meetingId && updates.Keys.Contains(a.MemberId))
                .ToListAsync();
            var existingByMember = existing.ToDictionary(a => a.MemberId, a => a);

            var updatedCount = 0;
            var insertedCount = 0;

            foreach (var kvp in updates)
            {
                if (existingByMember.TryGetValue(kvp.Key, out var attendance))
                {
                    attendance.IsPresent = kvp.Value;
                    attendance.MarkedAtUtc = now;
                    updatedCount++;
                }
                else
                {
                    _db.Attendances.Add(new Attendance
                    {
                        MeetingId = meetingId,
                        MemberId = kvp.Key,
                        IsPresent = kvp.Value,
                        MarkedAtUtc = now
                    });
                    insertedCount++;
                }
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Excel import completed. Updated: {updatedCount}. Added: {insertedCount}.";
            if (rowErrors.Count > 0)
                TempData["LayoutError"] = string.Join(" ", rowErrors.Take(5)) + (rowErrors.Count > 5 ? " ..." : string.Empty);
        }
        catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
        {
            TempData["LayoutError"] = "Database is currently unavailable. Import could not be completed.";
        }
        catch (Exception)
        {
            TempData["LayoutError"] = "Failed to import Excel file. Please verify the file format and try again.";
        }

        return RedirectToAction(nameof(MarkAttendance), new { id = meetingId });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttendanceImportTemplate(int meetingId)
    {
        var meetingExists = await _db.Meetings.AsNoTracking().AnyAsync(m => m.Id == meetingId);
        if (!meetingExists)
            return NotFound();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Attendance");

        ws.Cell(1, 1).Value = "MemberId";
        ws.Cell(1, 2).Value = "Present";

        ws.Cell(2, 1).Value = 1;
        ws.Cell(2, 2).Value = "True";

        var headerRange = ws.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#e9f2ff");
        ws.Columns(1, 2).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Meeting_{meetingId}_AttendanceImportTemplate.xlsx");
    }

    private void PopulateMeetingTitleOptions()
    {
        ViewBag.MeetingTitles = AllowedMeetingTitles
            .Select(title => new SelectListItem(title, title))
            .ToList();
    }

    private static bool IsValidMeetingTitle(string? title)
    {
        return !string.IsNullOrWhiteSpace(title)
            && AllowedMeetingTitles.Contains(title, StringComparer.OrdinalIgnoreCase);
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

    private static bool TryReadAttendanceHeaderMap(IXLWorksheet worksheet, out Dictionary<string, int> headers)
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

        return headers.ContainsKey("memberid") && headers.ContainsKey("present");
    }

    private static string GetCellValue(IXLRow row, IReadOnlyDictionary<string, int> headers, string headerKey)
    {
        if (!headers.TryGetValue(headerKey, out var col))
            return string.Empty;

        return row.Cell(col).GetValue<string>().Trim();
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var chars = header.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static bool TryParseBoolean(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "1":
            case "y":
            case "yes":
            case "true":
            case "t":
                value = true;
                return true;
            case "0":
            case "n":
            case "no":
            case "false":
            case "f":
                value = false;
                return true;
            default:
                return false;
        }
    }
}

public sealed class MeetingListRow
{
    public int Id { get; init; }
    public DateTime MeetingDate { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Location { get; init; }
    public required string MinutesOfMeeting { get; init; }
    public int PresentCount { get; init; }
    public int TotalMarked { get; init; }
}

