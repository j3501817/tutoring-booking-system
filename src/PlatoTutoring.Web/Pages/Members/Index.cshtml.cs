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
    private readonly StudentParentLinkingService _linkingService;

    public IndexModel(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService,
        StudentParentLinkingService linkingService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _bookingService = bookingService;
        _linkingService = linkingService;
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

    // 學生與家長列表篩選條件（學校、年別、班別獨立不連動）
    [BindProperty(SupportsGet = true)]
    public string? FilterSchool { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterGrade { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterClass { get; set; }

    public record ChildBindingItem(Student Student, string Relationship);
    public record FamilyGroupRow(List<AppUser> Parents, List<ChildBindingItem> Children);

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
        // 班別固定為資優班與精英班（不需連動學校）
        ClassOptions = fieldOptions
            .Where(o => o.Category == FieldCategories.Class && (o.Value == "資優班" || o.Value == "精英班"))
            .ToList();
        if (!ClassOptions.Any())
        {
            ClassOptions = new List<FieldOption>
            {
                new() { Category = FieldCategories.Class, Value = "資優班", SortOrder = 1, IsActive = true },
                new() { Category = FieldCategories.Class, Value = "精英班", SortOrder = 2, IsActive = true }
            };
        }
        SubjectOptions = fieldOptions.Where(o => o.Category == FieldCategories.Subject).ToList();

        // 建立「家長與學生關係」合併列表：
        // 關聯家長若有 2 位（或多位），家長區塊並列呈現 2 位家長，學生不拆成兩筆
        var rows = new List<FamilyGroupRow>();
        var visitedStudentIds = new HashSet<int>();
        var visitedParentIds = new HashSet<int>();

        // 1. 優先處理有綁定關係的學生群組
        foreach (var stu in Students)
        {
            if (visitedStudentIds.Contains(stu.Id)) continue;

            var studentLinks = AllLinks.Where(l => l.StudentId == stu.Id).ToList();
            if (!studentLinks.Any()) continue;

            var familyParentIds = studentLinks.Select(l => l.ParentUserId).Distinct().ToList();

            // 關聯此組家長的所有孩子
            var familyStudentIds = AllLinks
                .Where(l => familyParentIds.Contains(l.ParentUserId))
                .Select(l => l.StudentId)
                .Distinct()
                .ToList();

            var parentsForThisFamily = new List<AppUser>();
            foreach (var pid in familyParentIds)
            {
                var p = Parents.FirstOrDefault(x => x.Id == pid);
                if (p != null && !parentsForThisFamily.Any(x => x.Id == p.Id))
                {
                    parentsForThisFamily.Add(p);
                    visitedParentIds.Add(p.Id);
                }
            }

            var familyChildren = new List<ChildBindingItem>();
            foreach (var sid in familyStudentIds)
            {
                visitedStudentIds.Add(sid);
                var s = Students.FirstOrDefault(x => x.Id == sid);
                if (s != null)
                {
                    var rel = AllLinks.FirstOrDefault(l => l.StudentId == sid)?.Relationship ?? "家長";
                    familyChildren.Add(new ChildBindingItem(s, rel));
                }
            }

            rows.Add(new FamilyGroupRow(parentsForThisFamily, familyChildren));
        }

        // 2. 未綁定任何學生的家長
        foreach (var parent in Parents.Where(p => !visitedParentIds.Contains(p.Id)))
        {
            rows.Add(new FamilyGroupRow(new List<AppUser> { parent }, new List<ChildBindingItem>()));
        }

        // 3. 未綁定任何家長的孩子
        foreach (var unboundStudent in Students.Where(s => !visitedStudentIds.Contains(s.Id)))
        {
            rows.Add(new FamilyGroupRow(new List<AppUser>(), new List<ChildBindingItem>
            {
                new(unboundStudent, "未綁定")
            }));
        }
        // 套用學生多維篩選（學校、年別、班別獨立不連動）
        if (!string.IsNullOrWhiteSpace(FilterSchool) || !string.IsNullOrWhiteSpace(FilterGrade) || !string.IsNullOrWhiteSpace(FilterClass))
        {
            var filteredRows = new List<FamilyGroupRow>();
            foreach (var row in rows)
            {
                var matchedChildren = row.Children.Where(c =>
                {
                    var matchSchool = string.IsNullOrWhiteSpace(FilterSchool) || c.Student.School == FilterSchool;
                    var matchGrade = string.IsNullOrWhiteSpace(FilterGrade) || c.Student.Grade == FilterGrade;
                    var matchClass = string.IsNullOrWhiteSpace(FilterClass) || c.Student.ClassName == FilterClass;
                    return matchSchool && matchGrade && matchClass;
                }).ToList();

                if (matchedChildren.Any())
                {
                    filteredRows.Add(new FamilyGroupRow(row.Parents, matchedChildren));
                }
            }
            rows = filteredRows;
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
    // 1. 學生管理：新增 / 修改 / 刪除（含學校、年別、班別、三個手機欄位與自動電話關聯）
    // =========================================================================
    public async Task<IActionResult> OnPostCreateStudentAsync(
        string fullName,
        string school,
        string grade,
        string className,
        string? phone1,
        string? phone2,
        string? phone3,
        DateOnly enrollmentDate,
        int? parentUserId,
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
                Phone1 = phone1?.Trim() ?? string.Empty,
                Phone2 = phone2?.Trim() ?? string.Empty,
                Phone3 = phone3?.Trim() ?? string.Empty,
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
                    Relationship = "家長"
                });
                await _db.SaveChangesAsync();
            }

            // 雙向電話自動比對關聯
            var linkedCount = await _linkingService.AutoLinkStudentByPhonesAsync(student.Id);

            if (grantProRataBonus)
            {
                var sem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
                if (sem != null)
                {
                    var entry = await _ledgerService.GrantSemesterBonusAsync(student.Id, sem.Id, operatorUser.Id);
                    TempData["SuccessMessage"] = $"已新增學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」，自動比對關聯 {linkedCount} 位家長，並核發學期贈送堂數 +{entry.BonusDelta} 堂！";
                    return Redirect("/Members#tab-student-parent");
                }
            }

            var linkNote = linkedCount > 0 ? $"，電話自動比對成功關聯 {linkedCount} 位家長" : "（未比對到相同電話之已建檔家長，維持未綁定）";
            TempData["SuccessMessage"] = $"已新增學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」{linkNote}。";
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
        string? phone1,
        string? phone2,
        string? phone3,
        DateOnly enrollmentDate,
        int? parentUserId)
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
            student.Phone1 = phone1?.Trim() ?? string.Empty;
            student.Phone2 = phone2?.Trim() ?? string.Empty;
            student.Phone3 = phone3?.Trim() ?? string.Empty;
            student.SyncGradeClass();
            if (string.IsNullOrWhiteSpace(student.GradeClass))
            {
                student.GradeClass = "未分班";
            }
            if (enrollmentDate != default)
            {
                student.EnrollmentDate = enrollmentDate;
            }

            if (parentUserId.HasValue)
            {
                var existingLinks = await _db.ParentStudentLinks
                    .Where(l => l.StudentId == student.Id)
                    .ToListAsync();
                _db.ParentStudentLinks.RemoveRange(existingLinks);

                if (parentUserId.Value > 0)
                {
                    _db.ParentStudentLinks.Add(new ParentStudentLink
                    {
                        ParentUserId = parentUserId.Value,
                        StudentId = student.Id,
                        Relationship = "家長"
                    });
                }
            }

            await _db.SaveChangesAsync();

            // 執行手機比對自動綁定
            var linkedCount = await _linkingService.AutoLinkStudentByPhonesAsync(student.Id);

            var linkNote = linkedCount > 0 ? $"，電話比對已關聯 {linkedCount} 位家長" : "";
            TempData["SuccessMessage"] = $"已更新學生「{student.FullName}（{student.DisplaySchoolGradeClass}）」資料{linkNote}。";
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

            // 電話雙向比對自動關聯
            var linkedStudentsCount = await _linkingService.AutoLinkParentByPhoneAsync(parent.Id);
            var linkMsg = linkedStudentsCount > 0 ? $"，且電話比對自動關聯了 {linkedStudentsCount} 位學生" : "";

            TempData["SuccessMessage"] = $"已新增家長帳號：「{parent.DisplayName} ({parent.Username})」{linkMsg}。";
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

            // 電話雙向比對自動關聯
            var linkedStudentsCount = await _linkingService.AutoLinkParentByPhoneAsync(parent.Id);
            var linkMsg = linkedStudentsCount > 0 ? $"，電話比對已關聯 {linkedStudentsCount} 位學生" : "";

            TempData["SuccessMessage"] = $"已更新家長「{parent.DisplayName} ({parent.Username})」的資料{linkMsg}。";
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
