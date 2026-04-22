using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace KgmApp.Models;

public class Meeting
{
    public int Id { get; set; }

    [Display(Name = "Meeting Date")]
    [DataType(DataType.Date)]
    public DateTime MeetingDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(300)]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Minutes of the Meeting")]
    [StringLength(4000)]
    public string MinutesOfMeeting { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ValidateNever]
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

