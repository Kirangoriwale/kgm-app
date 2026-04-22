using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KgmApp.Models;

public class Attendance
{
    public int Id { get; set; }

    public int MeetingId { get; set; }

    public int MemberId { get; set; }

    public bool IsPresent { get; set; }

    public DateTime MarkedAtUtc { get; set; } = DateTime.UtcNow;

    [ValidateNever]
    public Meeting? Meeting { get; set; }

    [ValidateNever]
    public Member? Member { get; set; }
}

