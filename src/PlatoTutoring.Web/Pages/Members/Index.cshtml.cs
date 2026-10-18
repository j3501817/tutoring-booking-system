using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.Members;

[Authorize(Policy = "TeacherAccess")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CreditLedgerService _ledgerService;
    private readonly BookingService _bookingService;

    public IndexModel(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _bookingService = bookingService;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public List<Student> Students { get; set; } = new();
    public List<AppUser> Parents { get; set; } = new();
    public List<AppUser> Teachers { get; set; } = new();
    public List<ParentStudentLink> AllLinks { get; set; } = new();
    public Dictionary<int, int> TeacherSlotCounts { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToPage("/Account/Login");
        CurrentUser = user;

        await LoadPageDataAsync();
        return Page();
    }

    private async Task LoadPageDataAsync()
    {
        Students = await _db.Students
            .OrderBy(s => s.IsDroppedOut)
            .ThenBy(s => s.Id)
            .ToListAsync();

        var allUsers = await _db.Users
            .OrderBy(u => u.Id)
            .ToListAsync();

        Parents = allUsers.Where(u => u.Role == UserRole.Parent).ToList();
        Teachers = allUsers.Where(u => u.Role == UserRole.Teacher).ToList();

        AllLinks = await _db.ParentStudentLinks
            .Include(l => l.ParentUser)
            .Include(l => l.Student)
            .OrderBy(l => l.StudentId)
            .ThenBy(l => l.ParentUserId)
            .ToListAsync();

        TeacherSlotCounts = await _db.TutoringSlots
            .GroupBy(s => s.TeacherUserId)
            .Select(g => new { TeacherId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Count);
    }

    // =========================================================================
    // 1. 學生管理：新增 / 修改 / 刪除（含學校、年級、班級、選擇家長）
    // =========================================================================
    public async Task<IActionResult> OnPostCreateStudentAsync(
        string fullName,
        string school,
        string grade,
        string className,
        DateOnly enrollmentDate,
        int? parentUserId,
        string? relationship,
        bool grantProRataBonus)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["ErrorMessage"] = "請輸入學生姓名。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            var cleanSchool = school?.Trim() ?? string.Empty;
            var cleanGrade = grade?.Trim() ?? string.Empty;
            var cleanClass = className?.Trim() ?? string.Empty;
            var combinedGradeClass = $"{cleanGrade}{cleanClass}".Trim();
            if (string.IsNullOrWhiteSpace(combinedGradeClass))
            {
                combinedGradeClass = "未分班";
            }

            var student = new Student
            {
                FullName = fullName.Trim(),
                School = cleanSchool,
                Grade = cleanGrade,
                ClassName = cleanClass,
                GradeClass = combinedGradeClass.Length > 50 ? combinedGradeClass[..50] : combinedGradeClass,
                EnrollmentDate = enrollmentDate == default ? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)) : enrollmentDate
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            if (parentUserId.HasValue && parentUserId.Value > 0)
            {
                _db.ParentStudentLinks.Add(new ParentStudentLink
                {
                    ParentUserId = parentUserId.Value,
                    StudentId = student.Id,
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "家長" : relationship.Trim()
                });
                await _db.SaveChangesAsync();
            }

            if (grantProRataBonus)
            {
                var sem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
                if (sem != null)
                {
                    var entry = await _ledgerService.GrantSemesterBonusAsync(student.Id, sem.Id, operatorUser.Id);
                    TempData["SuccessMessage"] = $"已新增學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」，並自動核發學期贈送堂數 +{entry.BonusDelta} 堂！";
                    return Redirect("/Members#section-student-parent");
                }
            }

            TempData["SuccessMessage"] = $"已新增學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"新增學生失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    public async Task<IActionResult> OnPostUpdateStudentAsync(
        int studentId,
        string fullName,
        string school,
        string grade,
        string className,
        DateOnly enrollmentDate,
        int? parentUserId,
        string? relationship)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var student = await _db.Students.FindAsync(studentId);
        if (student == null)
        {
            TempData["ErrorMessage"] = "找不到指定的學生資料。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            student.FullName = string.IsNullOrWhiteSpace(fullName) ? student.FullName : fullName.Trim();
            student.School = school?.Trim() ?? string.Empty;
            student.Grade = grade?.Trim() ?? string.Empty;
            student.ClassName = className?.Trim() ?? string.Empty;
            student.SyncGradeClass();
            if (string.IsNullOrWhiteSpace(student.GradeClass))
            {
                student.GradeClass = "未分班";
            }
            if (enrollmentDate != default)
            {
                student.EnrollmentDate = enrollmentDate;
            }

            // 同步更新學生所屬家長關聯
            var existingLinks = await _db.ParentStudentLinks
                .Where(l => l.StudentId == student.Id)
                .ToListAsync();
            _db.ParentStudentLinks.RemoveRange(existingLinks);

            if (parentUserId.HasValue && parentUserId.Value > 0)
            {
                _db.ParentStudentLinks.Add(new ParentStudentLink
                {
                    ParentUserId = parentUserId.Value,
                    StudentId = student.Id,
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "家長" : relationship.Trim()
                });
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已更新學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」的資料與家長綁定。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"更新學生失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    public async Task<IActionResult> OnPostDeleteStudentAsync(int studentId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var student = await _db.Students.FindAsync(studentId);
        if (student == null)
        {
            TempData["ErrorMessage"] = "找不到指定的學生資料。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            var name = student.FullName;

            // 先釋放該學生有效預約所佔用的時段名額並清理子表紀錄（防止 Restrict 外鍵阻擋）
            var bookings = await _db.Bookings
                .Include(b => b.TutoringSlot)
                .Where(b => b.StudentId == studentId)
                .ToListAsync();
            foreach (var b in bookings)
            {
                if (b.Status == BookingStatus.Confirmed && b.TutoringSlot != null && b.TutoringSlot.CurrentBookedCount > 0)
                {
                    b.TutoringSlot.CurrentBookedCount--;
                }
            }
            _db.Bookings.RemoveRange(bookings);

            var waitlists = await _db.WaitlistEntries.Where(w => w.StudentId == studentId).ToListAsync();
            _db.WaitlistEntries.RemoveRange(waitlists);

            var homeworks = await _db.HomeworkWatchItems.Where(h => h.StudentId == studentId).ToListAsync();
            _db.HomeworkWatchItems.RemoveRange(homeworks);

            var ledgers = await _db.CreditLedgerEntries.Where(e => e.StudentId == studentId).ToListAsync();
            _db.CreditLedgerEntries.RemoveRange(ledgers);

            var topups = await _db.TopUpBatches.Where(t => t.StudentId == studentId).ToListAsync();
            _db.TopUpBatches.RemoveRange(topups);

            var settlements = await _db.DropoutSettlements.Where(d => d.StudentId == studentId).ToListAsync();
            _db.DropoutSettlements.RemoveRange(settlements);

            var links = await _db.ParentStudentLinks.Where(l => l.StudentId == studentId).ToListAsync();
            _db.ParentStudentLinks.RemoveRange(links);

            _db.Students.Remove(student);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已刪除學生「{name}」及其關聯紀錄。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"刪除學生失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    // =========================================================================
    // 2. 家長管理：新增 / 修改 / 刪除（與學生在同一頁面操作）
    // =========================================================================
    public async Task<IActionResult> OnPostCreateParentAsync(
        string displayName,
        string username,
        string password,
        string? phone,
        string? lineUserId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var cleanUsername = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanUsername) || string.IsNullOrWhiteSpace(displayName))
        {
            TempData["ErrorMessage"] = "請填寫家長姓名與登入帳號。";
            return Redirect("/Members#section-student-parent");
        }

        if (await _db.Users.AnyAsync(u => u.Username == cleanUsername))
        {
            TempData["ErrorMessage"] = $"登入帳號「{cleanUsername}」已存在，請使用其他帳號。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            var rawPwd = string.IsNullOrWhiteSpace(password) ? "demo123" : password.Trim();
            var parent = new AppUser
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(rawPwd),
                DisplayName = displayName.Trim(),
                Role = UserRole.Parent,
                Phone = phone?.Trim() ?? string.Empty,
                LineUserId = string.IsNullOrWhiteSpace(lineUserId) ? null : lineUserId.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Users.Add(parent);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已新增家長帳號：「{parent.DisplayName} ({parent.Username})」。現在可在左側學生區直接選擇此家長進行綁定！";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"新增家長失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    public async Task<IActionResult> OnPostUpdateParentAsync(
        int userId,
        string displayName,
        string username,
        string? newPassword,
        string? phone,
        string? lineUserId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Parent);
        if (parent == null)
        {
            TempData["ErrorMessage"] = "找不到指定的家長帳號。";
            return Redirect("/Members#section-student-parent");
        }

        var cleanUsername = username?.Trim() ?? parent.Username;
        if (!string.Equals(cleanUsername, parent.Username, StringComparison.OrdinalIgnoreCase) &&
            await _db.Users.AnyAsync(u => u.Username == cleanUsername && u.Id != userId))
        {
            TempData["ErrorMessage"] = $"帳號「{cleanUsername}」已被其他使用者使用。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            parent.DisplayName = string.IsNullOrWhiteSpace(displayName) ? parent.DisplayName : displayName.Trim();
            parent.Username = cleanUsername;
            parent.Phone = phone?.Trim() ?? string.Empty;
            parent.LineUserId = string.IsNullOrWhiteSpace(lineUserId) ? null : lineUserId.Trim();

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                parent.PasswordHash = PasswordHelper.HashPassword(newPassword.Trim());
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已更新家長「{parent.DisplayName} ({parent.Username})」的資料。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"更新家長失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    public async Task<IActionResult> OnPostDeleteParentAsync(int userId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Parent);
        if (parent == null)
        {
            TempData["ErrorMessage"] = "找不到指定的家長帳號。";
            return Redirect("/Members#section-student-parent");
        }

        try
        {
            var name = parent.DisplayName;

            var links = await _db.ParentStudentLinks.Where(l => l.ParentUserId == userId).ToListAsync();
            _db.ParentStudentLinks.RemoveRange(links);

            var waitlists = await _db.WaitlistEntries.Where(w => w.ParentUserId == userId).ToListAsync();
            _db.WaitlistEntries.RemoveRange(waitlists);

            var lineLogs = await _db.LineNotificationLogs.Where(l => l.RecipientParentUserId == userId).ToListAsync();
            _db.LineNotificationLogs.RemoveRange(lineLogs);

            _db.Users.Remove(parent);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已刪除家長帳號「{name}」並解除其學生綁定。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"刪除家長失敗：{ex.Message}";
        }

        return Redirect("/Members#section-student-parent");
    }

    // =========================================================================
    // 3. 老師管理：新增 / 修改 / 刪除
    // =========================================================================
    public async Task<IActionResult> OnPostCreateTeacherAsync(
        string displayName,
        string username,
        string password,
        string? phone)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var cleanUsername = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanUsername) || string.IsNullOrWhiteSpace(displayName))
        {
            TempData["ErrorMessage"] = "請填寫老師姓名與登入帳號。";
            return Redirect("/Members#section-teachers");
        }

        if (await _db.Users.AnyAsync(u => u.Username == cleanUsername))
        {
            TempData["ErrorMessage"] = $"登入帳號「{cleanUsername}」已存在，請使用其他帳號。";
            return Redirect("/Members#section-teachers");
        }

        try
        {
            var rawPwd = string.IsNullOrWhiteSpace(password) ? "demo123" : password.Trim();
            var teacher = new AppUser
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(rawPwd),
                DisplayName = displayName.Trim(),
                Role = UserRole.Teacher,
                Phone = phone?.Trim() ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Users.Add(teacher);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已新增老師帳號：「{teacher.DisplayName} ({teacher.Username})」。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"新增老師失敗：{ex.Message}";
        }

        return Redirect("/Members#section-teachers");
    }

    public async Task<IActionResult> OnPostUpdateTeacherAsync(
        int userId,
        string displayName,
        string username,
        string? newPassword,
        string? phone)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Teacher);
        if (teacher == null)
        {
            TempData["ErrorMessage"] = "找不到指定的老師帳號。";
            return Redirect("/Members#section-teachers");
        }

        var cleanUsername = username?.Trim() ?? teacher.Username;
        if (!string.Equals(cleanUsername, teacher.Username, StringComparison.OrdinalIgnoreCase) &&
            await _db.Users.AnyAsync(u => u.Username == cleanUsername && u.Id != userId))
        {
            TempData["ErrorMessage"] = $"帳號「{cleanUsername}」已被其他使用者使用。";
            return Redirect("/Members#section-teachers");
        }

        try
        {
            teacher.DisplayName = string.IsNullOrWhiteSpace(displayName) ? teacher.DisplayName : displayName.Trim();
            teacher.Username = cleanUsername;
            teacher.Phone = phone?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                teacher.PasswordHash = PasswordHelper.HashPassword(newPassword.Trim());
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已更新老師「{teacher.DisplayName} ({teacher.Username})」的資料。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"更新老師失敗：{ex.Message}";
        }

        return Redirect("/Members#section-teachers");
    }

    public async Task<IActionResult> OnPostDeleteTeacherAsync(int userId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        if (operatorUser.Id == userId)
        {
            TempData["ErrorMessage"] = "無法刪除您目前正在登入使用中的老師帳號。";
            return Redirect("/Members#section-teachers");
        }

        var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Teacher);
        if (teacher == null)
        {
            TempData["ErrorMessage"] = "找不到指定的老師帳號。";
            return Redirect("/Members#section-teachers");
        }

        try
        {
            var name = teacher.DisplayName;

            // 若該老師有排課、作業追蹤或儲值登錄紀錄，轉移至目前操作人或另一位老師/管理員以保留歷史帳務完整性
            var fallbackUser = await _db.Users
                .Where(u => u.Id != userId && (u.Role == UserRole.Teacher || u.Role == UserRole.Admin))
                .OrderBy(u => u.Role == UserRole.Teacher ? 0 : 1)
                .FirstOrDefaultAsync() ?? operatorUser;

            var slots = await _db.TutoringSlots.Where(s => s.TeacherUserId == userId).ToListAsync();
            foreach (var s in slots) s.TeacherUserId = fallbackUser.Id;

            var homeworks = await _db.HomeworkWatchItems.Where(h => h.TeacherUserId == userId).ToListAsync();
            foreach (var h in homeworks) h.TeacherUserId = fallbackUser.Id;

            var topups = await _db.TopUpBatches.Where(t => t.RecordedByTeacherId == userId).ToListAsync();
            foreach (var t in topups) t.RecordedByTeacherId = fallbackUser.Id;

            _db.Users.Remove(teacher);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已刪除老師帳號「{name}」。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"刪除老師失敗：{ex.Message}";
        }

        return Redirect("/Members#section-teachers");
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
