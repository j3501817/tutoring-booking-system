using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.Teacher;

[Authorize(Policy = "TeacherAccess")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookingService;
    private readonly CreditLedgerService _ledgerService;
    private readonly AttachmentStorageService _attachmentService;
    private readonly LineNotificationService _lineService;

    public IndexModel(
        AppDbContext db,
        BookingService bookingService,
        CreditLedgerService ledgerService,
        AttachmentStorageService attachmentService,
        LineNotificationService lineService)
    {
        _db = db;
        _bookingService = bookingService;
        _ledgerService = ledgerService;
        _attachmentService = attachmentService;
        _lineService = lineService;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public List<Student> ActiveStudents { get; set; } = new();
    public List<CourseProgress> ProgressList { get; set; } = new();
    public List<AttendancePurposeOption> PurposeOptions { get; set; } = new();
    public List<TutoringSlot> Slots { get; set; } = new();
    public List<Booking> AllConfirmedBookings { get; set; } = new();
    public List<TopUpBatch> RecentTopUps { get; set; } = new();
    public List<HomeworkWatchItem> HomeworkItems { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");
        CurrentUser = user;

        ActiveStudents = await _db.Students
            .Where(s => !s.IsDroppedOut)
            .OrderBy(s => s.GradeClass)
            .ThenBy(s => s.Id)
            .ToListAsync();

        ProgressList = await _db.CourseProgresses
            .OrderByDescending(p => p.IsCurrentProgress)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        PurposeOptions = await _db.AttendancePurposeOptions
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        Slots = await _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .Include(s => s.TeacherUser)
            .OrderBy(s => s.StartsAtUtc)
            .ToListAsync();

        AllConfirmedBookings = await _db.Bookings
            .Include(b => b.Student)
            .Include(b => b.TutoringSlot)
            .ThenInclude(s => s!.CourseProgress)
            .Where(b => b.Status == BookingStatus.Confirmed)
            .OrderBy(b => b.TutoringSlot!.StartsAtUtc)
            .ToListAsync();

        RecentTopUps = await _db.TopUpBatches
            .Include(t => t.Student)
            .Include(t => t.RecordedByTeacher)
            .Include(t => t.Attachment)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .ToListAsync();

        HomeworkItems = await _db.HomeworkWatchItems
            .Include(h => h.Student)
            .Include(h => h.CourseProgress)
            .OrderByDescending(h => h.FirstNotifiedAtUtc)
            .Take(10)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostRecordTopUpAsync(
        int studentId,
        decimal amountNtd,
        int creditsAdded,
        DateOnly remittanceDate,
        string accountLastFiveDigits,
        string? note,
        IFormFile? attachmentFile)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            int? attachmentId = null;
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                await using var stream = attachmentFile.OpenReadStream();
                var savedAtt = await _attachmentService.SaveAttachmentAsync(
                    attachmentFile.FileName,
                    attachmentFile.ContentType,
                    stream,
                    user.Id);
                attachmentId = savedAtt.Id;
            }

            await _ledgerService.RecordTopUpAsync(
                studentId,
                user.Id,
                amountNtd,
                creditsAdded,
                remittanceDate,
                accountLastFiveDigits,
                note ?? string.Empty,
                attachmentId);

            TempData["SuccessMessage"] = $"已成功登錄儲值：NT${amountNtd:N0}（+{creditsAdded} 堂，匯款末五碼 {accountLastFiveDigits}），並寫入不可覆寫流水帳。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAdjustCreditsAsync(
        int studentId,
        int paidDelta,
        int bonusDelta,
        string reason)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _ledgerService.ApplyManualAdjustmentAsync(
                studentId,
                user.Id,
                paidDelta,
                bonusDelta,
                reason);
            TempData["SuccessMessage"] = $"已完成堂數發放／沖正調整（付費 {paidDelta:+#;-#;0} 堂、贈送 {bonusDelta:+#;-#;0} 堂）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateSlotAsync(
        int courseProgressId,
        string gradeClass,
        SlotType slotType,
        DateOnly slotDate,
        TimeOnly startTime,
        int durationMinutes,
        int maxCapacity,
        bool isExtraSlot,
        string? notes)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            // 輸入時間為 Asia/Taipei (UTC+8)，轉換為 UTC 儲存
            var localStart = slotDate.ToDateTime(startTime);
            var startsAtUtc = DateTime.SpecifyKind(localStart.AddHours(-8), DateTimeKind.Utc);
            var endsAtUtc = startsAtUtc.AddMinutes(Math.Max(30, durationMinutes));

            await _bookingService.CreateSlotAsync(
                user.Id,
                courseProgressId,
                gradeClass,
                slotType,
                startsAtUtc,
                endsAtUtc,
                maxCapacity,
                isExtraSlot,
                notes ?? string.Empty);

            TempData["SuccessMessage"] = $"已建立輔導時段：{slotDate:yyyy/MM/dd} {startTime:HH:mm}（名額 {maxCapacity} 名）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostProxyBookAsync(
        int studentId,
        int slotId,
        string purposeName)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.CreateBookingAsync(slotId, studentId, user.Id, purposeName);
            TempData["SuccessMessage"] = $"老師代學生預約成功（已保存操作人：{user.DisplayName}）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecordAttendanceAsync(
        int bookingId,
        AttendanceStatus attendanceStatus,
        bool finalizeSlot)
    {
        try
        {
            await _bookingService.RecordAttendanceAsync(bookingId, attendanceStatus, finalizeSlot);
            TempData["SuccessMessage"] = $"已更新點名狀態為：{attendanceStatus}" + (finalizeSlot ? "（此時段已課後定案）" : "");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostFlagHomeworkAsync(
        int studentId,
        int courseProgressId,
        string subjectNote)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _lineService.FlagHomeworkPoorAsync(studentId, user.Id, courseProgressId, subjectNote);
            TempData["SuccessMessage"] = "已標記作業不佳並透過 LINE 發送預約輔導通知給家長！";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTrigger48hReminderAsync()
    {
        var count = await _lineService.Process48HourHomeworkRemindersAsync();
        TempData["SuccessMessage"] = $"已執行 48 小時作業未預約檢查，共發送 {count} 筆二次催促通知。";
        return RedirectToPage();
    }

    private async Task<AppUser?> GetCurrentTeacherOrAdminAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
