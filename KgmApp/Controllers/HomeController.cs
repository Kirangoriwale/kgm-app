using System.Diagnostics;
using KgmApp.Data;
using KgmApp.Models;
using KgmApp.Models.Meetings;
using KgmApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KgmApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;
        private readonly MemberContributionSummaryService _memberContribution;
        private readonly MemberUpiPaymentOptions _upiPayment;
        private readonly IConfiguration _configuration;

        public HomeController(
            ILogger<HomeController> logger,
            AppDbContext db,
            MemberContributionSummaryService memberContribution,
            IOptions<MemberUpiPaymentOptions> upiPayment,
            IConfiguration configuration)
        {
            _logger = logger;
            _db = db;
            _memberContribution = memberContribution;
            _upiPayment = upiPayment.Value;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var role = HttpContext.Session.GetString(AccountController.SessionKeyRole);
            if (string.Equals(role, "Member", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(MyDashboard));

            var vm = new DashboardViewModel();
            try
            {
                vm.TotalMembers = await _db.Members.AsNoTracking().CountAsync();
                vm.TotalSubMembers = await _db.SubMembers.AsNoTracking().CountAsync();
                var membersRegSubmitted = await _db.Members.AsNoTracking().CountAsync(m => m.IsRegistrationFormSubmitted);
                var subMembersRegSubmitted = await _db.SubMembers.AsNoTracking().CountAsync(s => s.IsRegistrationFormSubmitted);
                vm.RegistrationFormsSubmittedCount = membersRegSubmitted + subMembersRegSubmitted;

                var deposits = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Category == Transaction.CategoryCashDepositToBank)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                var withdrawals = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Category == Transaction.CategoryBankWithdrawal)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;

                var cashIncome = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Type == Transaction.TypeIncome && t.PaymentMode == Transaction.PaymentModeCash)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                var cashExpense = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Type == Transaction.TypeExpense && t.PaymentMode == Transaction.PaymentModeCash)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                vm.CashInHand = (cashIncome - cashExpense) - deposits + withdrawals;

                var onlineIncome = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Type == Transaction.TypeIncome && t.PaymentMode == Transaction.PaymentModeOnline)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                var onlineExpense = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Type == Transaction.TypeExpense && t.PaymentMode == Transaction.PaymentModeOnline)
                    .SumAsync(t => (decimal?)t.Amount) ?? 0m;
                vm.BankBalance = (onlineIncome - onlineExpense) + deposits - withdrawals;
            }
            catch
            {
                // Database unavailable (e.g. local dev without PostgreSQL)
            }

            return View(vm);
        }

        /// <summary>Legacy member route. Uses same data/view as MyDashboard.</summary>
        public async Task<IActionResult> MemberDashboard()
            => await MyDashboard();

        /// <summary>Personal dashboard for any logged-in member account (Admin/Treasurer/Secretary/Committee/Member).</summary>
        public async Task<IActionResult> MyDashboard()
        {
            var (memberId, memberName) = GetMemberFromSession();
            if (memberId == null)
                return RedirectToAction(nameof(Index));

            if (!await IsCurrentMemberRegistrationSubmittedAsync(memberId.Value))
                return RedirectToAction("RegistrationForm", "Account");

            var vm = await BuildMemberDashboardViewModelAsync(memberId.Value, memberName);
            ViewData["Title"] = "My dashboard";
            ViewData["PageTitle"] = "My Dashboard";
            ViewData["BreadcrumbCurrent"] = "My Dashboard";
            return View(nameof(MemberDashboard), vm);
        }

        /// <summary>Line items for half-yearly + registration payments for the logged-in member account.</summary>
        public async Task<IActionResult> MemberPaidDetails()
        {
            var (memberId, memberName) = GetMemberFromSession();
            if (memberId == null)
                return RedirectToAction(nameof(Index));

            if (!await IsCurrentMemberRegistrationSubmittedAsync(memberId.Value))
                return RedirectToAction("RegistrationForm", "Account");

            var vm = await _memberContribution.GetPaidDetailsAsync(memberId.Value, memberName);
            if (vm == null)
                return RedirectToAction(nameof(MemberDashboard));

            ViewData["Title"] = "Payment details";
            ViewData["PageTitle"] = "Total paid — details";
            ViewData["BreadcrumbCurrent"] = "Paid details";
            return View(vm);
        }

        /// <summary>Breakdown of expected vs paid for pending amounts for the logged-in member account.</summary>
        public async Task<IActionResult> MemberPendingDetails()
        {
            var (memberId, memberName) = GetMemberFromSession();
            if (memberId == null)
                return RedirectToAction(nameof(Index));

            if (!await IsCurrentMemberRegistrationSubmittedAsync(memberId.Value))
                return RedirectToAction("RegistrationForm", "Account");

            var vm = await _memberContribution.GetPendingDetailsAsync(memberId.Value, memberName);
            if (vm == null)
                return RedirectToAction(nameof(MemberDashboard));

            ViewData["Title"] = "Pending details";
            ViewData["PageTitle"] = "Amount pending — details";
            ViewData["BreadcrumbCurrent"] = "Pending details";
            return View(vm);
        }

        /// <summary>Per-meeting attendance for general meetings for the logged-in member account.</summary>
        public async Task<IActionResult> MemberGeneralMeetingAttendance()
        {
            var (memberId, memberName) = GetMemberFromSession();
            if (memberId == null)
                return RedirectToAction(nameof(Index));

            if (!await IsCurrentMemberRegistrationSubmittedAsync(memberId.Value))
                return RedirectToAction("RegistrationForm", "Account");

            var meetings = await _db.Meetings
                .AsNoTracking()
                .Where(m => EF.Functions.ILike(m.Title, MeetingAttendanceFilters.GeneralMeetingTitleLikePattern))
                .OrderByDescending(m => m.MeetingDate)
                .ThenByDescending(m => m.Id)
                .ToListAsync();

            var meetingIds = meetings.Select(m => m.Id).ToList();
            var presenceByMeeting = meetingIds.Count == 0
                ? new Dictionary<int, bool>()
                : await _db.Attendances
                    .AsNoTracking()
                    .Where(a => a.MemberId == memberId.Value && meetingIds.Contains(a.MeetingId))
                    .ToDictionaryAsync(a => a.MeetingId, a => a.IsPresent);

            var rows = meetings.Select(m =>
            {
                string presence;
                if (!presenceByMeeting.TryGetValue(m.Id, out var isPresent))
                    presence = "Not marked";
                else
                    presence = isPresent ? "Present" : "Absent";

                return new MemberGeneralMeetingAttendanceRowViewModel
                {
                    MeetingDate = m.MeetingDate,
                    Title = m.Title,
                    Location = string.IsNullOrWhiteSpace(m.Location) ? "—" : m.Location,
                    MinutesOfMeeting = m.MinutesOfMeeting,
                    PresenceText = presence
                };
            }).ToList();

            var vm = new MemberGeneralMeetingAttendanceViewModel
            {
                MemberName = memberName,
                Rows = rows
            };

            ViewData["Title"] = "Attendance — general meetings";
            ViewData["PageTitle"] = "Attendance — general meetings";
            ViewData["BreadcrumbCurrent"] = "Attendance";
            return View(vm);
        }

        private (int? Id, string Name) GetMemberFromSession()
        {
            var idStr = HttpContext.Session.GetString(AccountController.SessionKeyMemberId);
            if (!int.TryParse(idStr, out var id))
                return (null, string.Empty);
            var name = HttpContext.Session.GetString(AccountController.SessionKeyMemberName) ?? "Member";
            return (id, name);
        }

        private async Task<bool> IsCurrentMemberRegistrationSubmittedAsync(int memberId)
        {
            var member = await _db.Members
                .AsNoTracking()
                .Where(m => m.Id == memberId)
                .Select(m => new { m.IsRegistrationFormSubmitted })
                .FirstOrDefaultAsync();

            return member?.IsRegistrationFormSubmitted ?? false;
        }

        private async Task<MemberDashboardViewModel> BuildMemberDashboardViewModelAsync(int memberId, string memberName)
        {
            decimal paid = 0m, pending = 0m;
            try
            {
                (paid, pending) = await _memberContribution.GetPaidAndPendingAsync(memberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MemberDashboard: contribution summary failed for member {MemberId}", memberId);
            }

            int presentGeneral = 0, totalGeneral = 0;
            try
            {
                (presentGeneral, totalGeneral) = await GetGeneralMeetingAttendanceCountsAsync(memberId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MemberDashboard: general meeting attendance counts failed for member {MemberId}", memberId);
            }

            (bool Has, DateTime? Date, string Title, string Location) upcoming = (false, null, string.Empty, string.Empty);
            try
            {
                upcoming = await GetNextUpcomingGeneralMeetingAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MemberDashboard: upcoming general meeting query failed for member {MemberId}", memberId);
            }

            string? upiHref = null;
            try
            {
                upiHref = UpiPayLinkBuilder.BuildPendingDuesUri(_upiPayment.PayeeVpa, _upiPayment.PayeeName, pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MemberDashboard: UPI link build failed for member {MemberId}", memberId);
            }

            bool isMemberRegistrationSubmitted = false;
            IReadOnlyList<MemberDashboardSubMemberInfoViewModel> subMembers = Array.Empty<MemberDashboardSubMemberInfoViewModel>();
            try
            {
                var memberInfo = await _db.Members
                    .AsNoTracking()
                    .Where(m => m.Id == memberId)
                    .Select(m => new
                    {
                        m.IsRegistrationFormSubmitted,
                        SubMembers = m.SubMembers
                            .OrderBy(s => s.SrNo)
                            .ThenBy(s => s.Name)
                            .Select(s => new MemberDashboardSubMemberInfoViewModel
                            {
                                Name = s.Name,
                                IsRegistrationSubmitted = s.IsRegistrationFormSubmitted
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (memberInfo != null)
                {
                    isMemberRegistrationSubmitted = memberInfo.IsRegistrationFormSubmitted;
                    subMembers = memberInfo.SubMembers;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MemberDashboard: member info load failed for member {MemberId}", memberId);
            }

            return new MemberDashboardViewModel
            {
                MemberName = memberName,
                IsMemberRegistrationSubmitted = isMemberRegistrationSubmitted,
                SubMembers = subMembers,
                TotalPaid = paid,
                TotalPending = pending,
                UpiPayPendingHref = upiHref,
                GeneralMeetingsPresentCount = presentGeneral,
                GeneralMeetingsTotalCount = totalGeneral,
                HasUpcomingGeneralMeeting = upcoming.Has,
                UpcomingGeneralMeetingDate = upcoming.Date,
                UpcomingGeneralMeetingTitle = upcoming.Title,
                UpcomingGeneralMeetingLocation = upcoming.Location
            };
        }

        /// <summary>Present count / total meetings for titles matching general attendance report (ILIKE %general%).</summary>
        private async Task<(int PresentCount, int TotalMeetings)> GetGeneralMeetingAttendanceCountsAsync(int memberId)
        {
            var pattern = MeetingAttendanceFilters.GeneralMeetingTitleLikePattern;

            var total = await _db.Meetings
                .AsNoTracking()
                .CountAsync(m => EF.Functions.ILike(m.Title, pattern));

            var present = await _db.Attendances
                .AsNoTracking()
                .CountAsync(a =>
                    a.MemberId == memberId &&
                    a.IsPresent &&
                    _db.Meetings.Any(m =>
                        m.Id == a.MeetingId &&
                        EF.Functions.ILike(m.Title, pattern)));

            return (present, total);
        }

        /// <summary>Next general meeting after today’s calendar date (ILIKE %general%, earliest first).</summary>
        private async Task<(bool Has, DateTime? Date, string Title, string Location)> GetNextUpcomingGeneralMeetingAsync()
        {
            var todayUtc = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
            var meeting = await _db.Meetings
                .AsNoTracking()
                .Where(m =>
                    EF.Functions.ILike(m.Title, MeetingAttendanceFilters.GeneralMeetingTitleLikePattern) &&
                    m.MeetingDate > todayUtc)
                .OrderBy(m => m.MeetingDate)
                .ThenBy(m => m.Id)
                .FirstOrDefaultAsync();

            if (meeting == null)
                return (false, null, string.Empty, string.Empty);

            var location = string.IsNullOrWhiteSpace(meeting.Location) ? "—" : meeting.Location.Trim();
            return (true, meeting.MeetingDate, meeting.Title, location);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AboutUs()
        {
            ViewData["Title"] = "About Us";
            ViewData["PageTitle"] = "About Us";
            ViewData["BreadcrumbCurrent"] = "About Us";

            var row = await _db.AboutUsContents
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync();

            row ??= new AboutUsContent
            {
                ContentHtml = DefaultAboutUsHtml
            };

            return View(row);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAboutUs([Bind("Id,ContentHtml")] AboutUsContent model)
        {
            var html = (model.ContentHtml ?? string.Empty).Trim();
            if (string.Equals(html, "<p><br></p>", StringComparison.OrdinalIgnoreCase))
                html = string.Empty;

            if (string.IsNullOrWhiteSpace(html))
            {
                ModelState.AddModelError(nameof(AboutUsContent.ContentHtml), "About Us content is required.");
                ViewData["Title"] = "About Us";
                ViewData["PageTitle"] = "About Us";
                ViewData["BreadcrumbCurrent"] = "About Us";
                model.ContentHtml = html;
                return View("AboutUs", model);
            }

            var row = await _db.AboutUsContents
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync();

            if (row == null)
            {
                row = new AboutUsContent
                {
                    ContentHtml = html,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _db.AboutUsContents.Add(row);
            }
            else
            {
                row.ContentHtml = html;
                row.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "About Us content updated successfully.";
            return RedirectToAction(nameof(AboutUs));
        }

        public async Task<IActionResult> CommitteeMembers()
        {
            var rows = await _db.Members
                .AsNoTracking()
                .Where(m => m.IsCommiteeMember && m.IsActive)
                .Select(m => new CommitteeMemberCardViewModel
                {
                    MemberId = m.Id,
                    Name = m.Name,
                    Designation = string.IsNullOrWhiteSpace(m.Designation) ? "Committee Member" : m.Designation,
                    MobileNo = m.MobileNo
                })
                .ToListAsync();

            var designationPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // Marathi committee order
                ["अध्यक्ष"] = 1,
                ["उपाध्यक्ष"] = 2,
                ["सचिव"] = 3,
                ["उपसचिव"] = 4,
                ["खजिनदार"] = 5,
                ["उपखजिनदार"] = 6,
                ["सल्लागार"] = 7,
                ["सदस्य"] = 8,

                // English aliases (for backward compatibility)
                ["Chairman"] = 1,
                ["Vice Chairman"] = 2,
                ["Secretary"] = 3,
                ["Vice Secretary"] = 4,
                ["Treasurer"] = 5,
                ["Vice Treasurer"] = 6,
                ["Advisor"] = 7,
                ["Member"] = 8
            };

            rows = rows
                .OrderBy(x => designationPriority.TryGetValue(x.Designation.Trim(), out var rank) ? rank : int.MaxValue)
                .ThenBy(x => x.Name)
                .ToList();

            ViewData["Title"] = "Committee Members";
            ViewData["PageTitle"] = "Committee Members";
            ViewData["BreadcrumbCurrent"] = "Committee Members";
            var supabaseProjectUrl =
                _configuration["Supabase:ProjectUrl"]
                ?? _configuration["SUPABASE_URL"]
                ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
            if (!string.IsNullOrWhiteSpace(supabaseProjectUrl))
            {
                var photoBucket = _configuration["Supabase:MemberPhotosBucket"];
                if (string.IsNullOrWhiteSpace(photoBucket))
                {
                    photoBucket = "member-photos";
                }

                var photoFolder = _configuration["Supabase:MemberPhotosFolder"]?.Trim().Trim('/');
                var baseUrl = $"{supabaseProjectUrl.TrimEnd('/')}/storage/v1/object/public/{photoBucket}";
                if (!string.IsNullOrWhiteSpace(photoFolder))
                {
                    baseUrl = $"{baseUrl}/{photoFolder}";
                }

                ViewData["CommitteeMemberPhotoBaseUrl"] = baseUrl;
                ViewData["CommitteeMemberPhotoAltBaseUrl"] = $"{supabaseProjectUrl.TrimEnd('/')}/storage/files/buckets/{photoBucket}";
            }

            return View(rows);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private const string DefaultAboutUsHtml = """
<h1>खामगाव ग्रामस्थ मंडळ,मुंबई — परिचय</h1>
<p>खामगाव ग्रामस्थ मंडळ,मुंबई ही आपल्या गावातील सदस्यांना एकत्र आणणारी एक सामाजिक व सांस्कृतिक संस्था आहे.<br>या मंडळाची स्थापना समाजातील एकोपा, सहकार्य आणि परस्पर संवाद वाढवण्यासाठी करण्यात आली आहे.</p>
<p>मंडळाच्या माध्यमातून विविध सामाजिक, सांस्कृतिक व विकासात्मक उपक्रम राबविण्यात येतात.<br>सदस्यांच्या सहभागातून समाजात सकारात्मक बदल घडवून आणण्याचा आमचा प्रयत्न असतो.</p>
<p>नियमित बैठका, कार्यक्रम आणि उपक्रमांद्वारे सर्व सदस्यांना एकत्र येण्याची संधी मिळते.<br>यामुळे समाजात ऐक्य, विश्वास आणि बांधिलकी अधिक दृढ होते.</p>
<p>मंडळामध्ये पारदर्शकता आणि जबाबदारी यांना विशेष महत्त्व दिले जाते.<br>सदस्यांच्या योगदानातून विविध उपक्रम यशस्वीपणे पार पाडले जातात.</p>
<p>आर्थिक व्यवहारांची नोंद व्यवस्थित ठेवून सर्वांना स्पष्ट माहिती दिली जाते.<br>यामुळे सदस्यांमध्ये विश्वास आणि समाधान निर्माण होते.</p>
<p>मंडळाचे उद्दिष्ट केवळ कार्यक्रम आयोजित करणे नसून,<br>समाजातील प्रत्येक सदस्याला जोडून ठेवणे आणि एकमेकांना सहकार्य करणे हे आहे.</p>
<p>नवीन पिढीला योग्य दिशा देणे आणि समाजात चांगल्या मूल्यांची जपणूक करणे हेही आमचे ध्येय आहे.</p>
<p>खामगाव ग्रामस्थ मंडळ,मुंबई हे सर्व सदस्यांचे एक कुटुंब आहे,<br>जिथे प्रत्येकाचा सहभाग महत्त्वाचा आहे.</p>
<p><strong>एकत्र येऊया, एकत्र वाढूया.</strong></p>
""";
    }
}
