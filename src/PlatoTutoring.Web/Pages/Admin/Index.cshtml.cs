using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.Admin;

[Authorize(Policy = "AdminAccess")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CreditLedgerService _ledgerService;
    private readonly AttachmentStorageService _attachmentService;
    private readonly LineNotificationService _lineService;

    public IndexModel(
        AppDbContext db,
        CreditLedgerService ledgerService,
        AttachmentStorageService attachmentService,
        LineNotificationService lineService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _attachmentService = attachmentService;
        _lineService = lineService;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public Semester? CurrentSemester { get; set; }
    public List<CourseProgress> AllProgresses { get; set; } = new();
    public List<Student> AllStudents { get; set; } = new();
    public List<AppUser> ParentUsers { get; set; } = new();
    public List<AttendancePurposeOption> PurposeOptions { get; set; } = new();
    public List<DropoutSettlementPreview> SettlementPreviews { get; set; } = new();
    public List<DropoutSettlement> PastSettlements { get; set; } = new();
    public List<ReconciliationDiscrepancy> Discrepancies { get; set; } = new();
    public List<CreditLedgerEntry> RecentLedgerEntries { get; set; } = new();
    public List<LineNotificationLog> RecentLineLogs { get; set; } = new();

    // 營運月報表指標
    public decimal TotalTopUpAmountNtd { get; set; }
    public decimal TotalUnearnedRevenueNtd { get; set; }
    public double AttendanceRatePercent { get; set; }
    public double SlotUtilizationPercent { get; set; }
    public Dictionary<string, int> PurposeCounts { get; set; } = new();
    public int HomeworkTotalCount { get; set; }
    public int HomeworkResolvedCount { get; set; }
    public int LineSentTotal { get; set; }
    public int LineFailedTotal { get; set; }
    public bool IsGroupNoShowPenaltyEnabled { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");
        CurrentUser = admin;

        CurrentSemester = await _db.Semesters.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        AllProgresses = await _db.CourseProgresses.OrderByDescending(p => p.Id).ToListAsync();
        AllStudents = await _db.Students.OrderBy(s => s.Id).ToListAsync();
        ParentUsers = await _db.Users.Where(u => u.Role == UserRole.Parent).ToListAsync();
        PurposeOptions = await _db.AttendancePurposeOptions.OrderBy(p => p.SortOrder).ToListAsync();

        foreach (var stu in AllStudents.Where(s => !s.IsDroppedOut))
        {
            SettlementPreviews.Add(await _ledgerService.PreviewDropoutSettlementAsync(stu.Id));
        }

        PastSettlements = await _db.DropoutSettlements
            .Include(d => d.Student)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync();

        Discrepancies = await _ledgerService.RunNightlyReconciliationAsync();

        RecentLedgerEntries = await _db.CreditLedgerEntries
            .Include(e => e.Student)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ThenByDescending(e => e.Id)
            .Take(15)
            .ToListAsync();

        RecentLineLogs = await _db.LineNotificationLogs
            .Include(l => l.RecipientParentUser)
            .OrderByDescending(l => l.SentAtUtc)
            .Take(12)
            .ToListAsync();

        // 計算月報指標
        var batches = await _db.TopUpBatches.ToListAsync();
        TotalTopUpAmountNtd = batches.Sum(b => b.AmountNtd);
        TotalUnearnedRevenueNtd = batches.Sum(b => b.RemainingPaidCredits * b.UnitPriceNtd);

        var allBookings = await _db.Bookings.ToListAsync();
        var gradedBookings = allBookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.AttendanceStatus != AttendanceStatus.Pending)
            .ToList();
        AttendanceRatePercent = gradedBookings.Count > 0
            ? Math.Round(100.0 * gradedBookings.Count(b => b.AttendanceStatus == AttendanceStatus.Present) / gradedBookings.Count, 1)
            : 100.0;

        var allSlots = await _db.TutoringSlots.ToListAsync();
        var totalCap = allSlots.Sum(s => s.MaxCapacity);
        var totalBooked = allSlots.Sum(s => s.CurrentBookedCount);
        SlotUtilizationPercent = totalCap > 0
            ? Math.Round(100.0 * totalBooked / totalCap, 1)
            : 0.0;

        PurposeCounts = allBookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .GroupBy(b => b.PurposeName)
            .ToDictionary(g => g.Key, g => g.Count());

        HomeworkTotalCount = await _db.HomeworkWatchItems.CountAsync();
        HomeworkResolvedCount = await _db.HomeworkWatchItems.CountAsync(h => h.IsResolvedByBooking);

        LineSentTotal = await _db.LineNotificationLogs.CountAsync(l => l.Status == "Sent" || l.Status == "RetriedSuccess");
        LineFailedTotal = await _db.LineNotificationLogs.CountAsync(l => l.Status.StartsWith("Failed"));

        var penaltySetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GroupNoShowPenaltyEnabled");
        IsGroupNoShowPenaltyEnabled = string.Equals(penaltySetting?.Value, "true", StringComparison.OrdinalIgnoreCase);

        return Page();
    }

    public async Task<IActionResult> OnPostCreateStudentAsync(
        string fullName,
        string gradeClass,
        DateOnly enrollmentDate,
        int parentUserId,
        bool grantProRataBonus)
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");

        try
        {
            var student = new Student
            {
                FullName = fullName.Trim(),
                GradeClass = gradeClass.Trim(),
                EnrollmentDate = enrollmentDate
            };
            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            _db.ParentStudentLinks.Add(new ParentStudentLink
            {
                ParentUserId = parentUserId,
                StudentId = student.Id,
                Relationship = "家長"
            });
            await _db.SaveChangesAsync();

            if (grantProRataBonus)
            {
                var sem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
                if (sem != null)
                {
                    var entry = await _ledgerService.GrantSemesterBonusAsync(student.Id, sem.Id, admin.Id);
                    TempData["SuccessMessage"] = $"已新增學生「{student.FullName}」，並依入班日比例自動核發學期贈送堂數 +{entry.BonusDelta} 堂！";
                    return RedirectToPage();
                }
            }

            TempData["SuccessMessage"] = $"已新增學生「{student.FullName}」並關聯家長帳號。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSwitchCurrentProgressAsync(int progressId)
    {
        var all = await _db.CourseProgresses.ToListAsync();
        foreach (var p in all)
        {
            p.IsCurrentProgress = (p.Id == progressId);
        }
        await _db.SaveChangesAsync();

        var active = all.FirstOrDefault(p => p.Id == progressId);
        TempData["SuccessMessage"] = $"已切換目前開放預約進度為：{active?.Code} {active?.Title}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateProgressAsync(
        string code,
        string title,
        string gradeClass,
        DateOnly weekStartDate,
        DateOnly weekEndDate,
        bool setAsCurrent)
    {
        var sem = await _db.Semesters.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        if (sem == null)
        {
            TempData["ErrorMessage"] = "請先設定學期。";
            return RedirectToPage();
        }

        if (setAsCurrent)
        {
            var existing = await _db.CourseProgresses.ToListAsync();
            foreach (var p in existing) p.IsCurrentProgress = false;
        }

        _db.CourseProgresses.Add(new CourseProgress
        {
            SemesterId = sem.Id,
            Code = code.Trim(),
            Title = title.Trim(),
            GradeClass = gradeClass.Trim(),
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            IsCurrentProgress = setAsCurrent
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"已新增課程進度：{code} {title}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEndSemesterBonusExpiryAsync()
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");

        var sem = await _db.Semesters.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        if (sem == null) return RedirectToPage();

        var count = await _ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(sem.Id, admin.Id);
        TempData["SuccessMessage"] = $"已執行學期結束贈送堂數歸零，共清除 {count} 位學生的未用贈送堂數並寫入稽核流水帳。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExecuteDropoutAsync(int studentId, string? note)
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");

        try
        {
            var settlement = await _ledgerService.ExecuteDropoutSettlementAsync(
                studentId,
                admin.Id,
                note ?? "家長申請退班線下結算");

            TempData["SuccessMessage"] = $"已完成退班結算！應退付費堂數 {settlement.TotalRefundablePaidCredits} 堂，應退金額 NT${settlement.CalculatedRefundAmountNtd:N0}（已留存批次計價紀錄並將堂數歸零）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleNoShowPenaltyAsync(bool enabled)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "GroupNoShowPenaltyEnabled");
        if (setting != null)
        {
            setting.Value = enabled ? "true" : "false";
            await _db.SaveChangesAsync();
        }
        TempData["SuccessMessage"] = $"團體課爽約記點停權規則已設為：{(enabled ? "啟用（30天內爽約3次暫停預約14天）" : "關閉（預設）")}。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddPurposeAsync(string purposeName)
    {
        if (!string.IsNullOrWhiteSpace(purposeName))
        {
            var maxOrder = await _db.AttendancePurposeOptions.Select(p => (int?)p.SortOrder).MaxAsync() ?? 0;
            _db.AttendancePurposeOptions.Add(new AttendancePurposeOption
            {
                Name = purposeName.Trim(),
                SortOrder = maxOrder + 1,
                IsActive = true
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已新增到班目的選項：{purposeName.Trim()}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPurgeExpiredAttachmentsAsync()
    {
        var purged = await _attachmentService.PurgeExpiredAttachmentsAsync();
        TempData["SuccessMessage"] = $"已執行 90 天到期轉帳截圖清理政策，本次清除 {purged} 筆過期附件。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRetryLineAsync(int logId)
    {
        var ok = await _lineService.RetryFailedNotificationAsync(logId);
        TempData["SuccessMessage"] = ok ? "LINE 通知重試發送成功！" : "重試失敗：家長尚未綁定有效的 LINE User ID。";
        return RedirectToPage();
    }

    private async Task<AppUser?> GetCurrentAdminAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
