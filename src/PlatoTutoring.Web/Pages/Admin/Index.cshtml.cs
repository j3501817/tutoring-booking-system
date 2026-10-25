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
    private readonly BookingService _bookingService;
    private readonly AttachmentStorageService _attachmentService;
    private readonly LineNotificationService _lineService;
    private readonly DeploymentRuntimeInfo _runtimeInfo;

    public IndexModel(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService,
        AttachmentStorageService attachmentService,
        LineNotificationService lineService,
        DeploymentRuntimeInfo runtimeInfo)
    {
        _db = db;
        _ledgerService = ledgerService;
        _bookingService = bookingService;
        _attachmentService = attachmentService;
        _lineService = lineService;
        _runtimeInfo = runtimeInfo;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public string DatabaseStatusText => _runtimeInfo.DatabaseDescription;
    public string BlobStorageStatusText => _attachmentService.StorageModeDescription;

    public Semester? CurrentSemester { get; set; }
    public List<CourseProgress> AllProgresses { get; set; } = new();
    public List<Student> AllStudents { get; set; } = new();
    public List<AppUser> AllUsers { get; set; } = new();
    public List<AppUser> ParentUsers { get; set; } = new();
    public List<ParentStudentLink> AllParentStudentLinks { get; set; } = new();
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

    public async Task<IActionResult> OnGetAsync()
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");
        CurrentUser = admin;

        CurrentSemester = await _db.Semesters.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        AllProgresses = await _db.CourseProgresses.OrderByDescending(p => p.Id).ToListAsync();
        AllStudents = await _db.Students.OrderBy(s => s.Id).ToListAsync();
        AllUsers = await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.Id).ToListAsync();
        ParentUsers = AllUsers.Where(u => u.Role == UserRole.Parent).ToList();
        AllParentStudentLinks = await _db.ParentStudentLinks
            .Include(l => l.ParentUser)
            .Include(l => l.Student)
            .OrderBy(l => l.ParentUserId)
            .ThenBy(l => l.StudentId)
            .ToListAsync();

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

        return Page();
    }

    public async Task<IActionResult> OnPostCreateUserAsync(
        string username,
        string password,
        string displayName,
        UserRole role,
        string? phone,
        string? lineUserId,
        int? linkStudentId,
        string? relationship)
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");

        var cleanUsername = username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == cleanUsername))
        {
            TempData["ErrorMessage"] = $"登入帳號「{cleanUsername}」已存在，請使用其他帳號名稱。";
            return Redirect("/Admin#tab-admin-accounts");
        }

        try
        {
            var newUser = new AppUser
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(password),
                DisplayName = displayName.Trim(),
                Role = role,
                Phone = phone?.Trim() ?? string.Empty,
                LineUserId = string.IsNullOrWhiteSpace(lineUserId) ? null : lineUserId.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            if (role == UserRole.Parent && linkStudentId.HasValue && linkStudentId.Value > 0)
            {
                _db.ParentStudentLinks.Add(new ParentStudentLink
                {
                    ParentUserId = newUser.Id,
                    StudentId = linkStudentId.Value,
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "家長" : relationship.Trim()
                });
                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"已建立 {role} 登入帳號：「{newUser.DisplayName} ({newUser.Username})」（密碼已使用 PBKDF2 雜湊加密儲存）。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return Redirect("/Admin#tab-admin-accounts");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(
        int targetUserId,
        string newPassword,
        string? phone,
        string? lineUserId)
    {
        var user = await _db.Users.FindAsync(targetUserId);
        if (user == null)
        {
            TempData["ErrorMessage"] = "找不到指定的帳號。";
            return Redirect("/Admin#tab-admin-accounts");
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.PasswordHash = PasswordHelper.HashPassword(newPassword.Trim());
        }

        if (phone != null)
        {
            user.Phone = phone.Trim();
        }

        if (lineUserId != null)
        {
            user.LineUserId = string.IsNullOrWhiteSpace(lineUserId) ? null : lineUserId.Trim();
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已更新帳號「{user.DisplayName} ({user.Username})」的密碼與聯絡設定。";
        return Redirect("/Admin#tab-admin-accounts");
    }

    public async Task<IActionResult> OnPostLinkParentStudentAsync(
        int parentUserId,
        int studentId,
        string? relationship)
    {
        var exists = await _db.ParentStudentLinks.AnyAsync(l =>
            l.ParentUserId == parentUserId && l.StudentId == studentId);

        if (exists)
        {
            TempData["ErrorMessage"] = "該家長帳號已經綁定此學生，無須重複綁定。";
            return Redirect("/Admin#tab-admin-accounts");
        }

        _db.ParentStudentLinks.Add(new ParentStudentLink
        {
            ParentUserId = parentUserId,
            StudentId = studentId,
            Relationship = string.IsNullOrWhiteSpace(relationship) ? "家長" : relationship.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "已成功新增「家長 ↔ 學生」授權綁定關係！";
        return Redirect("/Admin#tab-admin-accounts");
    }

    public async Task<IActionResult> OnPostUnlinkParentStudentAsync(int linkId)
    {
        var link = await _db.ParentStudentLinks.FindAsync(linkId);
        if (link != null)
        {
            _db.ParentStudentLinks.Remove(link);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "已解除該筆家長與學生的綁定關聯。";
        }
        return Redirect("/Admin#tab-admin-accounts");
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
                    return Redirect("/Admin#tab-admin-students");
                }
            }

            TempData["SuccessMessage"] = $"已新增學生「{student.FullName}」並關聯家長帳號。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return Redirect("/Admin#tab-admin-students");
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
        return Redirect("/Admin#tab-admin-semester");
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
            return Redirect("/Admin#tab-admin-semester");
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
        return Redirect("/Admin#tab-admin-semester");
    }

    public async Task<IActionResult> OnPostEndSemesterBonusExpiryAsync()
    {
        var admin = await GetCurrentAdminAsync();
        if (admin == null) return RedirectToPage("/Account/Login");

        var sem = await _db.Semesters.OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        if (sem == null) return Redirect("/Admin#tab-admin-semester");

        var count = await _ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(sem.Id, admin.Id);
        TempData["SuccessMessage"] = $"已執行學期結束贈送堂數歸零，共清除 {count} 位學生的未用贈送堂數並寫入稽核流水帳。";
        return Redirect("/Admin#tab-admin-semester");
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

        return Redirect("/Admin#tab-admin-dropout");
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
        return Redirect("/Admin#tab-admin-students");
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
        return Redirect("/Admin#tab-admin-audit");
    }

    public override async Task OnPageHandlerExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next)
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);
        var admin = await GetCurrentAdminAsync();
        if (admin == null)
        {
            TempData["ErrorMessage"] = "僅限系統管理員或已勾選「代理管理權限」之老師可使用管理員營運與報表。";
            context.Result = RedirectToPage("/Teacher/Index");
            return;
        }
        CurrentUser = admin;
        await next();
    }

    private async Task<AppUser?> GetCurrentAdminAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;
        if (user.Role == UserRole.Admin || (user.Role == UserRole.Teacher && user.IsProxyAdmin))
        {
            return user;
        }
        return null;
    }
}
