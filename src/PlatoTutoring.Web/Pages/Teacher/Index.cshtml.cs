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

    // 時段列表篩選與分頁
    [BindProperty(SupportsGet = true)]
    public string? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public SlotType? SlotTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int SlotsPage { get; set; } = 1;

    public int SlotsTotalPages { get; set; } = 1;
    public int SlotsTotalCount { get; set; } = 0;
    public List<TutoringSlot> PagedSlots { get; set; } = new();

    // 今日點名
    [BindProperty(SupportsGet = true)]
    public string? RollcallDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SelectedSlotId { get; set; }

    // 儲值列表篩選條件（學校、年別、班別、匯款日）
    [BindProperty(SupportsGet = true)]
    public string? FilterSchool { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterGrade { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterClass { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterRemittanceDate { get; set; }

    public List<TutoringSlot> TodaySlots { get; set; } = new();
    public List<Booking> TodayBookings { get; set; } = new();
    public TutoringSlot? ActiveRollcallSlot { get; set; }

    // 學生篩選選項（學校、年級、班級）
    public List<FieldOption> SchoolOptions { get; set; } = new();
    public List<FieldOption> GradeOptions { get; set; } = new();
    public List<FieldOption> ClassOptions { get; set; } = new();

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

        SchoolOptions = await _db.FieldOptions
            .Where(f => f.Category == FieldCategories.School && f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();

        GradeOptions = await _db.FieldOptions
            .Where(f => f.Category == FieldCategories.Grade && f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();

        ClassOptions = await _db.FieldOptions
            .Where(f => f.Category == FieldCategories.Class && f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();

        var todayDate = DateOnly.FromDateTime(DateTime.Today);
        ProgressList = await _db.CourseProgresses
            .Where(p => p.WeekEndDate >= todayDate)
            .OrderByDescending(p => p.IsCurrentProgress)
            .ThenByDescending(p => p.WeekStartDate)
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

        var topUpQuery = _db.TopUpBatches
            .Include(t => t.Student)
            .Include(t => t.RecordedByTeacher)
            .Include(t => t.Attachment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(FilterSchool))
        {
            topUpQuery = topUpQuery.Where(t => t.Student != null && t.Student.School == FilterSchool);
        }
        if (!string.IsNullOrWhiteSpace(FilterGrade))
        {
            topUpQuery = topUpQuery.Where(t => t.Student != null && t.Student.Grade == FilterGrade);
        }
        if (!string.IsNullOrWhiteSpace(FilterClass))
        {
            topUpQuery = topUpQuery.Where(t => t.Student != null && t.Student.ClassName == FilterClass);
        }
        if (!string.IsNullOrWhiteSpace(FilterRemittanceDate) && DateOnly.TryParse(FilterRemittanceDate, out var parsedRemitDate))
        {
            topUpQuery = topUpQuery.Where(t => t.RemittanceDate == parsedRemitDate);
        }

        RecentTopUps = await topUpQuery
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        var nowUtc = DateTime.UtcNow;
        var todayTaipeiDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var todayStartUtc = DateTime.SpecifyKind(todayTaipeiDate.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);

        // 已預約解決且課程時段已結束者不再顯示在列表中
        var endedBookingIds = await _db.Bookings
            .Where(b => b.TutoringSlot != null && b.TutoringSlot.EndsAtUtc <= nowUtc)
            .Select(b => b.Id)
            .ToListAsync();

        HomeworkItems = await _db.HomeworkWatchItems
            .Include(h => h.Student)
            .Include(h => h.CourseProgress)
            .Where(h => !h.IsResolvedByBooking || (h.ResolvedBookingId.HasValue && !endedBookingIds.Contains(h.ResolvedBookingId.Value)))
            .OrderByDescending(h => h.FirstNotifiedAtUtc)
            .Take(50)
            .ToListAsync();

        // 處理今日課堂點名時段
        var nowTpe = BookingService.ToTaipeiTime(DateTime.UtcNow);
        if (string.IsNullOrWhiteSpace(RollcallDate))
        {
            RollcallDate = nowTpe.ToString("yyyy-MM-dd");
        }

        if (DateOnly.TryParse(RollcallDate, out var parsedRollcallDate))
        {
            var rStartUtc = DateTime.SpecifyKind(parsedRollcallDate.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);
            var rEndUtc = DateTime.SpecifyKind(parsedRollcallDate.ToDateTime(TimeOnly.MaxValue).AddHours(-8), DateTimeKind.Utc);

            TodaySlots = await _db.TutoringSlots
                .Include(s => s.CourseProgress)
                .Include(s => s.TeacherUser)
                .Where(s => s.StartsAtUtc >= rStartUtc && s.StartsAtUtc <= rEndUtc)
                .OrderBy(s => s.StartsAtUtc)
                .ToListAsync();

            var todaySlotIds = TodaySlots.Select(s => s.Id).ToList();
            TodayBookings = await _db.Bookings
                .Include(b => b.Student)
                .Where(b => todaySlotIds.Contains(b.TutoringSlotId) && b.Status == BookingStatus.Confirmed)
                .ToListAsync();

            if (SelectedSlotId.HasValue)
            {
                ActiveRollcallSlot = TodaySlots.FirstOrDefault(s => s.Id == SelectedSlotId.Value);
            }
            if (ActiveRollcallSlot == null && TodaySlots.Any())
            {
                ActiveRollcallSlot = TodaySlots.First();
                SelectedSlotId = ActiveRollcallSlot.Id;
            }
        }

        // 處理時段列表分頁與篩選：預設顯示今天（含今天）以後的時段
        var slotsQuery = _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .Include(s => s.TeacherUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(DateFrom) && DateOnly.TryParse(DateFrom, out var dFrom))
        {
            var utcFrom = DateTime.SpecifyKind(dFrom.ToDateTime(TimeOnly.MinValue).AddHours(-8), DateTimeKind.Utc);
            slotsQuery = slotsQuery.Where(s => s.StartsAtUtc >= utcFrom);
        }
        else
        {
            // 列表預設顯示今天（含今天）以後的時段
            slotsQuery = slotsQuery.Where(s => s.StartsAtUtc >= todayStartUtc);
        }

        if (!string.IsNullOrWhiteSpace(DateTo) && DateOnly.TryParse(DateTo, out var dTo))
        {
            var utcTo = DateTime.SpecifyKind(dTo.ToDateTime(TimeOnly.MaxValue).AddHours(-8), DateTimeKind.Utc);
            slotsQuery = slotsQuery.Where(s => s.StartsAtUtc <= utcTo);
        }

        if (SlotTypeFilter.HasValue)
        {
            slotsQuery = slotsQuery.Where(s => s.SlotType == SlotTypeFilter.Value);
        }

        SlotsTotalCount = await slotsQuery.CountAsync();
        SlotsTotalPages = Math.Max(1, (int)Math.Ceiling(SlotsTotalCount / 10.0));
        if (SlotsPage < 1) SlotsPage = 1;
        if (SlotsPage > SlotsTotalPages) SlotsPage = SlotsTotalPages;

        // 排序以時間升冪排序，優先顯示可預約（尚有名額）的課堂
        PagedSlots = await slotsQuery
            .OrderBy(s => s.CurrentBookedCount >= s.MaxCapacity ? 1 : 0)
            .ThenBy(s => s.StartsAtUtc)
            .Skip((SlotsPage - 1) * 10)
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
        List<int> courseProgressIds,
        string grade,
        SlotType slotType,
        DateOnly slotDate,
        TimeOnly startTime,
        string? notes)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        if (courseProgressIds == null || courseProgressIds.Count == 0)
        {
            TempData["ErrorMessage"] = "請至少選擇一項課程進度。";
            return RedirectToPage();
        }

        try
        {
            var localStart = slotDate.ToDateTime(startTime);
            var startsAtUtc = DateTime.SpecifyKind(localStart.AddHours(-8), DateTimeKind.Utc);
            // 固定 2 小時 (120 分鐘)
            var endsAtUtc = startsAtUtc.AddMinutes(120);

            // 依據輔導類型固定名額：小組指導 4 人，團體輔導 20 人
            var maxCapacity = (slotType == SlotType.GroupCounseling ? 20 : 4);

            var createdCount = 0;
            foreach (var progressId in courseProgressIds)
            {
                await _bookingService.CreateSlotAsync(
                    user.Id,
                    progressId,
                    grade?.Trim() ?? string.Empty,
                    slotType,
                    startsAtUtc,
                    endsAtUtc,
                    maxCapacity,
                    false, // 不需要標記為考前加開
                    notes ?? string.Empty);
                createdCount++;
            }

            var typeName = slotType == SlotType.GroupCounseling ? "團體輔導" : "小組指導";
            TempData["SuccessMessage"] = $"已成功建立 {createdCount} 筆輔導時段（{typeName}，固定 2 小時，容納名額 {maxCapacity} 人）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateSlotAsync(
        int slotId,
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
            var localStart = slotDate.ToDateTime(startTime);
            var startsAtUtc = DateTime.SpecifyKind(localStart.AddHours(-8), DateTimeKind.Utc);
            var endsAtUtc = startsAtUtc.AddMinutes(Math.Max(30, durationMinutes));

            await _bookingService.UpdateSlotAsync(
                slotId,
                courseProgressId,
                gradeClass,
                slotType,
                startsAtUtc,
                endsAtUtc,
                maxCapacity,
                isExtraSlot,
                notes ?? string.Empty);

            TempData["SuccessMessage"] = $"輔導時段 #{slotId} 已成功更新。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.DeleteSlotAsync(slotId);
            TempData["SuccessMessage"] = $"輔導時段 #{slotId} 已成功刪除。";
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
        string purposeName,
        bool waiveCreditDeduction = false)
    {
        var user = await GetCurrentTeacherOrAdminAsync();
        if (user == null) return RedirectToPage("/Account/Login");

        try
        {
            await _bookingService.CreateBookingAsync(
                slotId,
                studentId,
                user.Id,
                purposeName,
                waiveCreditDeduction: waiveCreditDeduction);

            var waiveMsg = waiveCreditDeduction ? "（此筆為免扣堂數預約）" : "";
            TempData["SuccessMessage"] = $"老師代學生預約成功{waiveMsg}（已保存操作人：{user.DisplayName}）。";
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

    public async Task<IActionResult> OnPostBatchRecordAttendanceAsync(
        int slotId,
        List<int> bookingIds,
        List<AttendanceStatus> statuses,
        bool finalizeSlot)
    {
        try
        {
            if (bookingIds == null || statuses == null || bookingIds.Count != statuses.Count)
            {
                throw new InvalidOperationException("點名資料不完整。");
            }

            var items = new List<(int BookingId, AttendanceStatus Status)>();
            for (int i = 0; i < bookingIds.Count; i++)
            {
                items.Add((bookingIds[i], statuses[i]));
            }

            await _bookingService.BatchRecordAttendanceAsync(slotId, items, finalizeSlot);
            TempData["SuccessMessage"] = $"全班 {items.Count} 位學生點名已成功儲存！" + (finalizeSlot ? "（已設定為課後定案）" : "");
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

