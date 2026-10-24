using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.Parent;

[Authorize(Policy = "ParentAccess")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CreditLedgerService _ledgerService;
    private readonly BookingService _bookingService;

    public IndexModel(AppDbContext db, CreditLedgerService ledgerService, BookingService bookingService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _bookingService = bookingService;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public List<Student> AuthorizedStudents { get; set; } = new();
    public Student? SelectedStudent { get; set; }
    public CourseProgress? CurrentProgress { get; set; }
    public List<AttendancePurposeOption> PurposeOptions { get; set; } = new();
    public List<TutoringSlot> AvailableSlots { get; set; } = new();
    public List<DateOnly> SlotDates { get; set; } = new();
    public DateOnly? SelectedDate { get; set; }
    public List<Booking> StudentBookings { get; set; } = new();
    public List<WaitlistEntry> StudentWaitlistEntries { get; set; } = new();
    public List<CreditLedgerEntry> StudentLedgerEntries { get; set; } = new();
    public List<HomeworkWatchItem> OpenHomeworkItems { get; set; } = new();
    public List<LineNotificationLog> RecentLineLogs { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? studentId, string? date)
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);
        await _bookingService.ProcessExpiredWaitlistHoldsAsync();

        var user = await GetCurrentParentOrAdminAsync();
        if (user == null)
            return RedirectToPage("/Account/Login");

        CurrentUser = user;

        if (user.Role == UserRole.Admin)
        {
            AuthorizedStudents = await _db.Students
                .Where(s => !s.IsDroppedOut)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }
        else
        {
            AuthorizedStudents = await _db.ParentStudentLinks
                .Where(l => l.ParentUserId == user.Id && l.Student != null)
                .Select(l => l.Student!)
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        SelectedStudent = studentId.HasValue
            ? AuthorizedStudents.FirstOrDefault(s => s.Id == studentId.Value) ?? AuthorizedStudents.FirstOrDefault()
            : AuthorizedStudents.FirstOrDefault();

        CurrentProgress = await _db.CourseProgresses
            .Include(p => p.Semester)
            .FirstOrDefaultAsync(p => p.IsCurrentProgress);

        PurposeOptions = await _db.AttendancePurposeOptions
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        var allCurrentSlots = await _db.TutoringSlots
            .Include(s => s.TeacherUser)
            .Include(s => s.CourseProgress)
            .Where(s => s.CourseProgress != null && s.CourseProgress.IsCurrentProgress)
            .OrderBy(s => s.StartsAtUtc)
            .ToListAsync();

        SlotDates = allCurrentSlots
            .Select(s => DateOnly.FromDateTime(BookingService.ToTaipeiTime(s.StartsAtUtc)))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedDate))
        {
            SelectedDate = parsedDate;
            AvailableSlots = allCurrentSlots
                .Where(s => DateOnly.FromDateTime(BookingService.ToTaipeiTime(s.StartsAtUtc)) == parsedDate)
                .ToList();
        }
        else
        {
            AvailableSlots = allCurrentSlots;
        }

        if (SelectedStudent != null)
        {
            StudentBookings = await _db.Bookings
                .Include(b => b.TutoringSlot)
                .ThenInclude(s => s!.CourseProgress)
                .Include(b => b.TutoringSlot)
                .ThenInclude(s => s!.TeacherUser)
                .Where(b => b.StudentId == SelectedStudent.Id)
                .OrderByDescending(b => b.CreatedAtUtc)
                .ToListAsync();

            StudentWaitlistEntries = await _db.WaitlistEntries
                .Include(w => w.TutoringSlot)
                .ThenInclude(s => s!.CourseProgress)
                .Where(w => w.StudentId == SelectedStudent.Id)
                .OrderByDescending(w => w.CreatedAtUtc)
                .ToListAsync();

            StudentLedgerEntries = await _db.CreditLedgerEntries
                .Where(e => e.StudentId == SelectedStudent.Id)
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

            OpenHomeworkItems = await _db.HomeworkWatchItems
                .Include(h => h.CourseProgress)
                .Where(h => h.StudentId == SelectedStudent.Id && !h.IsResolvedByBooking)
                .OrderByDescending(h => h.FirstNotifiedAtUtc)
                .ToListAsync();
        }

        RecentLineLogs = await _db.LineNotificationLogs
            .Where(l => user.Role == UserRole.Admin || l.RecipientParentUserId == user.Id)
            .OrderByDescending(l => l.SentAtUtc)
            .Take(8)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostBookSlotAsync(int studentId, int slotId, string purposeName)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.CreateBookingAsync(slotId, studentId, user.Id, purposeName);
            TempData["SuccessMessage"] = $"已完成預約（到班目的：{purposeName}）！系統已自動按規則優先扣抵贈送堂數。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostJoinWaitlistAsync(int studentId, int slotId, string purposeName)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            var entry = await _bookingService.JoinWaitlistAsync(slotId, studentId, user.Id, purposeName);
            TempData["SuccessMessage"] = $"已登記候補（目前候補順位：第 {entry.Position} 順位）。名額釋出時將發送 LINE 通知並為您保留 30 分鐘。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostConfirmWaitlistAsync(int studentId, int waitlistEntryId)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.ConfirmWaitlistHoldAsync(waitlistEntryId, user.Id);
            TempData["SuccessMessage"] = "候補確認成功！已轉為正式預約並完成扣堂。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostCancelBookingAsync(int studentId, int bookingId, string? cancelReason)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            var (_, refunded) = await _bookingService.CancelBookingAsync(
                bookingId,
                user.Id,
                cancelReason ?? "家長於網站取消預約");

            TempData["SuccessMessage"] = refunded
                ? "預約已取消（距離開課滿 24 小時，原扣抵堂數已全數退回並產生沖正流水帳）。"
                : "預約已取消（距離開課未滿 24 小時，依規不退還堂數）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostRescheduleAsync(int studentId, int existingBookingId, int targetSlotId)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.RescheduleBookingAsync(existingBookingId, targetSlotId, user.Id);
            TempData["SuccessMessage"] = "同進度改期成功！原預約已退還並轉入新時段。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostBindLineAsync(int? studentId, string lineUserId)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        user.LineUserId = string.IsNullOrWhiteSpace(lineUserId) ? null : lineUserId.Trim();
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "已更新家長 LINE 通知綁定識別碼（僅用於發送通知）。";
        return RedirectToPage(new { studentId });
    }

    public async Task<IActionResult> OnPostUnbindLineAsync(int? studentId)
    {
        var user = await GetCurrentParentOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        user.LineUserId = null;
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = "已成功解除 LINE 官方帳號通知綁定。";
        return RedirectToPage(new { studentId });
    }

    private async Task<AppUser?> GetCurrentParentOrAdminAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
