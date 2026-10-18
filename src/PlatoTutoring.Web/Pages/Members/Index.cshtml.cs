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
    public const int PageSize = 10;

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

    // 系統管理 > 欄位管理 下拉與多選選項
    public List<FieldOption> SchoolOptions { get; set; } = new();
    public List<FieldOption> GradeOptions { get; set; } = new();
    public List<FieldOption> ClassOptions { get; set; } = new();
    public List<FieldOption> RelationshipOptions { get; set; } = new();
    public List<FieldOption> SubjectOptions { get; set; } = new();

    // 學生與家長合併列表（同家長不拆成兩筆）與分頁 (10筆/頁)
    public List<FamilyGroupRow> AllFamilyRows { get; set; } = new();
    public List<FamilyGroupRow> PagedFamilyRows { get; set; } = new();
    public int StudentParentPage { get; set; } = 1;
    public int StudentParentTotalPages { get; set; } = 1;

    // 老師列表分頁 (10筆/頁)
    public List<AppUser> PagedTeachers { get; set; } = new();
    public int TeacherPage { get; set; } = 1;
    public int TeacherTotalPages { get; set; } = 1;

    public record ChildBindingItem(Student Student, string Relationship);
    public record FamilyGroupRow(AppUser? Parent, List<ChildBindingItem> Children);

    public async Task<IActionResult> OnGetAsync(int sp = 1, int tp = 1)
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);

        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToPage("/Account/Login");
        CurrentUser = user;

        await LoadPageDataAsync(sp, tp);
        return Page();
    }

    private async Task LoadPageDataAsync(int sp, int tp)
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
            .OrderBy(l => l.ParentUserId)
            .ThenBy(l => l.StudentId)
            .ToListAsync();

        TeacherSlotCounts = await _db.TutoringSlots
            .GroupBy(s => s.TeacherUserId)
            .Select(g => new { TeacherId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TeacherId, x => x.Count);

        var fieldOptions = await _db.FieldOptions
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToListAsync();

        SchoolOptions = fieldOptions.Where(o => o.Category == FieldCategories.School).ToList();
        GradeOptions = fieldOptions.Where(o => o.Category == FieldCategories.Grade).ToList();
        ClassOptions = fieldOptions.Where(o => o.Category == FieldCategories.Class).ToList();
        RelationshipOptions = fieldOptions.Where(o => o.Category == FieldCategories.Relationship).ToList();
        SubjectOptions = fieldOptions.Where(o => o.Category == FieldCategories.Subject).ToList();

        // 建立「家長與學生關係」合併列表：同一位家長底下的多個學生顯示在同一筆
        var rows = new List<FamilyGroupRow>();
        var boundStudentIds = new HashSet<int>();

        foreach (var parent in Parents)
        {
            var links = AllLinks
                .Where(l => l.ParentUserId == parent.Id && l.Student != null)
                .Select(l =>
                {
                    boundStudentIds.Add(l.StudentId);
                    return new ChildBindingItem(l.Student!, l.Relationship ?? "家長");
                })
                .ToList();

            rows.Add(new FamilyGroupRow(parent, links));
        }

        // 未綁定任何家長的學生亦顯示於列表中，避免遺漏
        foreach (var unboundStudent in Students.Where(s => !boundStudentIds.Contains(s.Id)))
        {
            rows.Add(new FamilyGroupRow(null, new List<ChildBindingItem>
            {
                new(unboundStudent, "未綁定")
            }));
        }

        AllFamilyRows = rows;

        StudentParentTotalPages = Math.Max(1, (int)Math.Ceiling(AllFamilyRows.Count / (double)PageSize));
        StudentParentPage = Math.Clamp(sp, 1, StudentParentTotalPages);
        PagedFamilyRows = AllFamilyRows
            .Skip((StudentParentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        TeacherTotalPages = Math.Max(1, (int)Math.Ceiling(Teachers.Count / (double)PageSize));
        TeacherPage = Math.Clamp(tp, 1, TeacherTotalPages);
        PagedTeachers = Teachers
            .Skip((TeacherPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    // =========================================================================
    // 1. 學生管理：新增 / 修改 / 刪除（含學校、年別、連動班級、家長與關係稱謂下拉）
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
            return Redirect("/Members#tab-student-parent");
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
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "媽媽" : relationship.Trim()
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
                    return Redirect("/Members#tab-student-parent");
                }
            }

            TempData["SuccessMessage"] = $"已新增學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"新增學生失敗：{ex.Message}";
        }

        return Redirect("/Members#tab-student-parent");
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
            return Redirect("/Members#tab-student-parent");
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
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "媽媽" : relationship.Trim()
                });
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已更新學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」的資料與家長綁定。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"更新學生失敗：{ex.Message}";
        }

        return Redirect("/Members#tab-student-parent");
    }

    public async Task<IActionResult> OnPostDeleteStudentAsync(int studentId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var student = await _db.Students.FindAsync(studentId);
        if (student == null)
        {
            TempData["ErrorMessage"] = "找不到指定的學生資料。";
            return Redirect("/Members#tab-student-parent");
        }

        try
        {
            var name = student.FullName;

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

        return Redirect("/Members#tab-student-parent");
    }

    // =========================================================================
    // 2. 家長管理：新增 / 修改 / 刪除（彈跳視窗）
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
            return Redirect("/Members#tab-student-parent");
        }

        if (await _db.Users.AnyAsync(u => u.Username == cleanUsername))
        {
            TempData["ErrorMessage"] = $"登入帳號「{cleanUsername}」已存在，請使用其他帳號。";
            return Redirect("/Members#tab-student-parent");
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

            TempData["SuccessMessage"] = $"已新增家長帳號：「{parent.DisplayName} ({parent.Username})」。";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"新增家長失敗：{ex.Message}";
        }

        return Redirect("/Members#tab-student-parent");
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
            return Redirect("/Members#tab-student-parent");
        }

        var cleanUsername = username?.Trim() ?? parent.Username;
        if (!string.Equals(cleanUsername, parent.Username, StringComparison.OrdinalIgnoreCase) &&
            await _db.Users.AnyAsync(u => u.Username == cleanUsername && u.Id != userId))
        {
            TempData["ErrorMessage"] = $"帳號「{cleanUsername}」已被其他使用者使用。";
            return Redirect("/Members#tab-student-parent");
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

        return Redirect("/Members#tab-student-parent");
    }

    public async Task<IActionResult> OnPostDeleteParentAsync(int userId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Parent);
        if (parent == null)
        {
            TempData["ErrorMessage"] = "找不到指定的家長帳號。";
            return Redirect("/Members#tab-student-parent");
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

        return Redirect("/Members#tab-student-parent");
    }

    // =========================================================================
    // 3. 老師管理：新增 / 修改 / 刪除（含任教科目多選與代理管理權限勾選）
    // =========================================================================
    public async Task<IActionResult> OnPostCreateTeacherAsync(
        string displayName,
        string username,
        string password,
        string? phone,
        List<string>? subjects,
        bool isProxyAdmin = false)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var cleanUsername = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanUsername) || string.IsNullOrWhiteSpace(displayName))
        {
            TempData["ErrorMessage"] = "請填寫老師姓名與登入帳號。";
            return Redirect("/Members#tab-teachers");
        }

        if (await _db.Users.AnyAsync(u => u.Username == cleanUsername))
        {
            TempData["ErrorMessage"] = $"登入帳號「{cleanUsername}」已存在，請使用其他帳號。";
            return Redirect("/Members#tab-teachers");
        }

        try
        {
            var rawPwd = string.IsNullOrWhiteSpace(password) ? "demo123" : password.Trim();
            var joinedSubjects = subjects != null && subjects.Count > 0
                ? string.Join(",", subjects.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
                : string.Empty;

            var teacher = new AppUser
            {
                Username = cleanUsername,
                PasswordHash = PasswordHelper.HashPassword(rawPwd),
                DisplayName = displayName.Trim(),
                Role = UserRole.Teacher,
                Phone = phone?.Trim() ?? string.Empty,
                Subjects = joinedSubjects,
                IsProxyAdmin = isProxyAdmin,
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

        return Redirect("/Members#tab-teachers");
    }

    public async Task<IActionResult> OnPostUpdateTeacherAsync(
        int userId,
        string displayName,
        string username,
        string? newPassword,
        string? phone,
        List<string>? subjects,
        bool isProxyAdmin = false)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Teacher);
        if (teacher == null)
        {
            TempData["ErrorMessage"] = "找不到指定的老師帳號。";
            return Redirect("/Members#tab-teachers");
        }

        var cleanUsername = username?.Trim() ?? teacher.Username;
        if (!string.Equals(cleanUsername, teacher.Username, StringComparison.OrdinalIgnoreCase) &&
            await _db.Users.AnyAsync(u => u.Username == cleanUsername && u.Id != userId))
        {
            TempData["ErrorMessage"] = $"帳號「{cleanUsername}」已被其他使用者使用。";
            return Redirect("/Members#tab-teachers");
        }

        try
        {
            teacher.DisplayName = string.IsNullOrWhiteSpace(displayName) ? teacher.DisplayName : displayName.Trim();
            teacher.Username = cleanUsername;
            teacher.Phone = phone?.Trim() ?? string.Empty;
            teacher.Subjects = subjects != null && subjects.Count > 0
                ? string.Join(",", subjects.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
                : string.Empty;
            teacher.IsProxyAdmin = isProxyAdmin;

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

        return Redirect("/Members#tab-teachers");
    }

    public async Task<IActionResult> OnPostDeleteTeacherAsync(int userId)
    {
        var operatorUser = await GetCurrentUserAsync();
        if (operatorUser == null) return RedirectToPage("/Account/Login");

        if (operatorUser.Id == userId)
        {
            TempData["ErrorMessage"] = "無法刪除您目前正在登入使用中的老師帳號。";
            return Redirect("/Members#tab-teachers");
        }

        var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Teacher);
        if (teacher == null)
        {
            TempData["ErrorMessage"] = "找不到指定的老師帳號。";
            return Redirect("/Members#tab-teachers");
        }

        try
        {
            var name = teacher.DisplayName;

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

        return Redirect("/Members#tab-teachers");
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var userId)) return null;
        return await _db.Users.FindAsync(userId);
    }
}
