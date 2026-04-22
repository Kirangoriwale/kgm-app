using KgmApp.Data;
using KgmApp.Models;
using KgmApp.Models.Meetings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

