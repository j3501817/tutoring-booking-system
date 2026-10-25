using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.SystemAdmin;

[Authorize(Policy = "AdminAccess")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly CreditLedgerService _ledgerService;
    private readonly BookingService _bookingService;
    private readonly IScheduleManagerService _scheduleManager;

    public IndexModel(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService,
        IScheduleManagerService scheduleManager)
    {
        _db = db;
        _ledgerService = ledgerService;
        _bookingService = bookingService;
        _scheduleManager = scheduleManager;
    }

    public AppUser CurrentUser { get; set; } = null!;
    public List<FieldOption> AllOptions { get; set; } = new();
    public List<FieldOption> SchoolOptions { get; set; } = new();
    public List<FieldOption> GradeOptions { get; set; } = new();
    public List<FieldOption> ClassOptions { get; set; } = new(); // 班別
    public List<FieldOption> SubjectOptions { get; set; } = new();
    public List<AttendancePurposeOption> PurposeOptions { get; set; } = new(); // 到班目的
    public List<AcademicHoliday> AcademicHolidays { get; set; } = new(); // 休假日
    public Dictionary<DateOnly, AcademicHoliday> HolidayMap { get; set; } = new();
    public List<Semester> AllSemesters { get; set; } = new(); // 所有學期列表
    public Semester? CurrentSemester { get; set; }
    public Semester? ViewingSemester { get; set; }
    public int? SelectedSemesterId { get; set; }

    public IReadOnlyList<ScheduledTaskItem> ScheduledTasks { get; set; } = new List<ScheduledTaskItem>();
    public IReadOnlyList<ScheduledTaskLog> ScheduledLogs { get; set; } = new List<ScheduledTaskLog>();

    public int CalendarYear { get; set; }
    public int CalendarMonth { get; set; }
    public int PrevYear { get; set; }
    public int PrevMonth { get; set; }
    public int NextYear { get; set; }
    public int NextMonth { get; set; }
    public int StartDayOfWeek { get; set; }
    public int DaysInMonth { get; set; }

    public override async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);
        var admin = await GetCurrentAuthorizedUserAsync();
        if (admin == null)
        {
            TempData["ErrorMessage"] = "僅限系統管理員或已勾選「代理管理權限」之老師可使用系統管理。";
            context.Result = RedirectToPage("/Teacher/Index");
            return;
        }
        CurrentUser = admin;
        await next();
    }

    public async Task OnGetAsync(int? year = null, int? month = null, int? semesterId = null)
    {
        AllOptions = await _db.FieldOptions
            .OrderBy(o => o.Category)
            .ThenBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToListAsync();

        SchoolOptions = AllOptions.Where(o => o.Category == FieldCategories.School).ToList();
        GradeOptions = AllOptions.Where(o => o.Category == FieldCategories.Grade).ToList();
        ClassOptions = AllOptions.Where(o => o.Category == FieldCategories.Class).ToList();
        SubjectOptions = AllOptions.Where(o => o.Category == FieldCategories.Subject).ToList();

        PurposeOptions = await _db.AttendancePurposeOptions
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();

        AcademicHolidays = await _db.AcademicHolidays
            .Where(h => h.IsActive)
            .OrderBy(h => h.Date)
            .ToListAsync();

        HolidayMap = AcademicHolidays.ToDictionary(h => h.Date, h => h);

        await EnforceSingleCurrentSemesterAsync();
        await _db.SaveChangesAsync();

        AllSemesters = await _db.Semesters
            .OrderBy(s => s.StartDate)
            .ToListAsync();

        CurrentSemester = AllSemesters.FirstOrDefault(s => s.IsCurrent)
            ?? AllSemesters.OrderByDescending(s => s.StartDate).FirstOrDefault();

        SelectedSemesterId = semesterId ?? CurrentSemester?.Id;
        ViewingSemester = (SelectedSemesterId.HasValue ? AllSemesters.FirstOrDefault(s => s.Id == SelectedSemesterId.Value) : null)
            ?? CurrentSemester;

        if (year.HasValue && month.HasValue && month.Value >= 1 && month.Value <= 12)
        {
            CalendarYear = year.Value;
            CalendarMonth = month.Value;
        }
        else
        {
            var targetDate = ViewingSemester != null
                ? new DateTime(ViewingSemester.StartDate.Year, ViewingSemester.StartDate.Month, 1)
                : DateTime.Today;

            // 若目前月份在學期內則顯示當前月，否則顯示學期開始月份
            var today = DateTime.Today;
            if (ViewingSemester != null && DateOnly.FromDateTime(today) >= ViewingSemester.StartDate && DateOnly.FromDateTime(today) <= ViewingSemester.EndDate)
            {
                CalendarYear = today.Year;
                CalendarMonth = today.Month;
            }
            else
            {
                CalendarYear = targetDate.Year;
                CalendarMonth = targetDate.Month;
            }
        }

        DaysInMonth = DateTime.DaysInMonth(CalendarYear, CalendarMonth);
        StartDayOfWeek = (int)new DateOnly(CalendarYear, CalendarMonth, 1).DayOfWeek;

        var prev = new DateTime(CalendarYear, CalendarMonth, 1).AddMonths(-1);
        PrevYear = prev.Year;
        PrevMonth = prev.Month;

        var next = new DateTime(CalendarYear, CalendarMonth, 1).AddMonths(1);
        NextYear = next.Year;
        NextMonth = next.Month;

        await _scheduleManager.LoadConfigurationsFromDbAsync();
        ScheduledTasks = _scheduleManager.GetScheduledTasks();
    }

    public async Task<IActionResult> OnPostTriggerScheduleAsync(string jobCode)
    {
        var admin = await GetCurrentAuthorizedUserAsync();
        var operatorName = admin?.DisplayName ?? "系統管理員";
        var (isSuccess, message) = await _scheduleManager.TriggerJobAsync(jobCode, $"管理員 ({operatorName}) 手動");

        if (isSuccess)
        {
            TempData["SuccessMessage"] = message;
        }
        else
        {
            TempData["ErrorMessage"] = message;
        }

        return RedirectToPage(new { sub = "schedules" });
    }

    public async Task<IActionResult> OnPostToggleScheduleAsync(string jobCode, bool isEnabled)
    {
        await _scheduleManager.ToggleTaskAsync(jobCode, isEnabled);
        TempData["SuccessMessage"] = $"排程狀態已切換為：{(isEnabled ? "啟用" : "停用")}。";
        return RedirectToPage(new { sub = "schedules" });
    }

    public async Task<IActionResult> OnPostUpdateScheduleConfigAsync(
        string jobCode,
        bool isEnabled,
        ScheduleFrequencyType frequencyType,
        string dailyTime,
        int intervalMinutes)
    {
        await _scheduleManager.UpdateTaskConfigAsync(jobCode, isEnabled, frequencyType, dailyTime, intervalMinutes);
        TempData["SuccessMessage"] = "排程執行週期與設定已成功儲存！";
        return RedirectToPage(new { sub = "schedules" });
    }

    public async Task<IActionResult> OnPostCreateOptionAsync(
        string category,
        string value,
        int sortOrder = 1,
        string? activeTab = null)
    {
        var targetTab = activeTab ?? GetDefaultTabForCategory(category);
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "請填寫完整的欄位類別與選項名稱。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        var trimmedCat = category.Trim();
        var trimmedVal = value.Trim();

        var exists = await _db.FieldOptions.AnyAsync(o =>
            o.Category == trimmedCat &&
            o.Value == trimmedVal);

        if (exists)
        {
            TempData["ErrorMessage"] = $"該類別下已存在相同的選項「{trimmedVal}」。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        if (sortOrder <= 0)
        {
            var maxSort = await _db.FieldOptions
                .Where(o => o.Category == trimmedCat)
                .Select(o => (int?)o.SortOrder)
                .MaxAsync() ?? 0;
            sortOrder = maxSort + 1;
        }

        _db.FieldOptions.Add(new FieldOption
        {
            Category = trimmedCat,
            Value = trimmedVal,
            ParentValue = null,
            SortOrder = sortOrder,
            IsActive = true
        });

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已新增欄位選項：{GetCategoryDisplayName(trimmedCat)} － {trimmedVal}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostUpdateOptionAsync(
        int optionId,
        string value,
        int sortOrder = 1,
        string? activeTab = null)
    {
        var opt = await _db.FieldOptions.FindAsync(optionId);
        if (opt == null)
        {
            TempData["ErrorMessage"] = "找不到指定的欄位選項。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: activeTab ?? "tab-school");
        }

        var targetTab = activeTab ?? GetDefaultTabForCategory(opt.Category);
        if (string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "選項名稱不可為空白。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        opt.Value = value.Trim();
        opt.ParentValue = null;
        opt.SortOrder = sortOrder > 0 ? sortOrder : 1;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已更新欄位選項：{GetCategoryDisplayName(opt.Category)} － {opt.Value}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostDeleteOptionAsync(int optionId, string? activeTab = null)
    {
        var opt = await _db.FieldOptions.FindAsync(optionId);
        if (opt == null)
        {
            TempData["ErrorMessage"] = "找不到指定的欄位選項。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: activeTab ?? "tab-school");
        }

        var targetTab = activeTab ?? GetDefaultTabForCategory(opt.Category);
        var label = $"{GetCategoryDisplayName(opt.Category)} － {opt.Value}";
        _db.FieldOptions.Remove(opt);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已刪除欄位選項：{label}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostCreatePurposeAsync(string name, string? description, int sortOrder = 1, string? activeTab = null)
    {
        var targetTab = activeTab ?? "tab-purpose";
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "到班目的名稱不可為空白。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        var trimmedName = name.Trim();
        var exists = await _db.AttendancePurposeOptions.AnyAsync(p => p.Name == trimmedName);
        if (exists)
        {
            TempData["ErrorMessage"] = $"已存在相同的到班目的「{trimmedName}」。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        if (sortOrder <= 0)
        {
            var maxSort = await _db.AttendancePurposeOptions.MaxAsync(p => (int?)p.SortOrder) ?? 0;
            sortOrder = maxSort + 1;
        }

        _db.AttendancePurposeOptions.Add(new AttendancePurposeOption
        {
            Name = trimmedName,
            SortOrder = sortOrder,
            IsActive = true
        });

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已新增到班目的：{trimmedName}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostUpdatePurposeAsync(int id, string name, int sortOrder = 1, string? activeTab = null)
    {
        var targetTab = activeTab ?? "tab-purpose";
        var purpose = await _db.AttendancePurposeOptions.FindAsync(id);
        if (purpose == null)
        {
            TempData["ErrorMessage"] = "找不到指定的到班目的。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "到班目的名稱不可為空白。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        purpose.Name = name.Trim();
        purpose.SortOrder = sortOrder > 0 ? sortOrder : 1;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已更新到班目的：{purpose.Name}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostDeletePurposeAsync(int id, string? activeTab = null)
    {
        var targetTab = activeTab ?? "tab-purpose";
        var purpose = await _db.AttendancePurposeOptions.FindAsync(id);
        if (purpose == null)
        {
            TempData["ErrorMessage"] = "找不到指定的到班目的。";
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
        }

        var name = purpose.Name;
        _db.AttendancePurposeOptions.Remove(purpose);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已刪除到班目的：{name}";
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: targetTab);
    }

    public async Task<IActionResult> OnPostSaveSemesterAsync(
        int? semesterId,
        string? name,
        DateTime startDate,
        DateTime endDate,
        bool setAsCurrent = false)
    {
        if (endDate <= startDate)
        {
            TempData["ErrorMessage"] = "學期結束日期必須晚於開始日期。";
            return RedirectToPage();
        }

        var startOnly = DateOnly.FromDateTime(startDate);
        var endOnly = DateOnly.FromDateTime(endDate);

        Semester? semester = null;
        if (semesterId.HasValue && semesterId.Value > 0)
        {
            semester = await _db.Semesters.FindAsync(semesterId.Value);
        }

        var isNew = semester == null;
        var totalSemesters = await _db.Semesters.CountAsync();
        if (totalSemesters == 0)
        {
            // 系統首個學期強制設定為目前學期
            setAsCurrent = true;
        }

        if (isNew)
        {
            semester = new Semester
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"{startDate:yyyy} 學年度" : name.Trim(),
                StartDate = startOnly,
                EndDate = endOnly,
                IsCurrent = setAsCurrent,
                DefaultBonusCredits = 3
            };
            _db.Semesters.Add(semester);
            await _db.SaveChangesAsync();
        }
        else if (semester != null)
        {
            if (!string.IsNullOrWhiteSpace(name)) semester.Name = name.Trim();
            semester.StartDate = startOnly;
            semester.EndDate = endOnly;

            // 防呆：若取消勾選目前學期，但系統中並無其他進行中學期，則阻止並強制維持為目前學期
            if (!setAsCurrent && semester.IsCurrent)
            {
                var otherCurrentExists = await _db.Semesters.AnyAsync(s => s.Id != semester.Id && s.IsCurrent);
                if (!otherCurrentExists)
                {
                    if (TempData != null) TempData["ErrorMessage"] = "系統必須保留至少一個目前進行中之學期，不可直接取消。若欲更換，請直接將目標學期設為目前學期。";
                    setAsCurrent = true;
                }
                else
                {
                    semester.IsCurrent = false;
                }
            }
        }

        if (setAsCurrent)
        {
            var admin = await GetCurrentAuthorizedUserAsync();
            var oldCurrent = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent && s.Id != semester!.Id);
            if (oldCurrent != null && admin != null)
            {
                var expiredCount = await _ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(oldCurrent.Id, admin.Id);
                if (TempData != null) TempData["SuccessMessage"] = $"已設定「{semester!.Name}」為目前學期！前學期（{oldCurrent.Name}）之贈送堂數已全數結算歸零（共 {expiredCount} 位學生調整）。";
            }
            await EnforceSingleCurrentSemesterAsync(semester!.Id);
        }
        else
        {
            await EnforceSingleCurrentSemesterAsync();
        }

        await _db.SaveChangesAsync();
        if (TempData != null) TempData["SuccessMessage"] = $"已儲存學期「{semester!.Name}」期間：{semester.StartDate:yyyy-MM-dd} ～ {semester.EndDate:yyyy-MM-dd}";
        return RedirectToPage(new { semesterId = semester?.Id });
    }

    public async Task<IActionResult> OnPostSwitchSemesterAsync(int semesterId)
    {
        var targetSemester = await _db.Semesters.FindAsync(semesterId);
        if (targetSemester == null)
        {
            if (TempData != null) TempData["ErrorMessage"] = "找不到指定的學期。";
            return RedirectToPage();
        }

        var admin = await GetCurrentAuthorizedUserAsync();
        var oldCurrent = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent && s.Id != semesterId);
        if (oldCurrent != null && admin != null)
        {
            // 換學期：將前學期贈送堂數歸 0
            var expiredCount = await _ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(oldCurrent.Id, admin.Id);
            if (TempData != null) TempData["SuccessMessage"] = $"已切換至「{targetSemester.Name}」為目前學期！已將前學期（{oldCurrent.Name}）之贈送堂數全數結算歸零（共 {expiredCount} 位學生調整）。";
        }
        else
        {
            if (TempData != null) TempData["SuccessMessage"] = $"已切換至「{targetSemester.Name}」為目前學期。";
        }

        await EnforceSingleCurrentSemesterAsync(semesterId);
        await _db.SaveChangesAsync();

        return RedirectToPage(new { semesterId });
    }

    /// <summary>
    /// 保障當前學期（IsCurrent）唯一性防呆機制：
    /// 確保資料庫中永遠至多且至少有 1 個學期的 IsCurrent 為 true。
    /// </summary>
    private async Task EnforceSingleCurrentSemesterAsync(int? targetCurrentSemesterId = null)
    {
        var all = await _db.Semesters.ToListAsync();
        if (!all.Any()) return;

        if (targetCurrentSemesterId.HasValue)
        {
            foreach (var s in all)
            {
                s.IsCurrent = (s.Id == targetCurrentSemesterId.Value);
            }
        }
        else
        {
            var currents = all.Where(s => s.IsCurrent).OrderByDescending(s => s.StartDate).ToList();
            if (currents.Count > 1)
            {
                // 若有複數個學期同時為 true，只保留開始日最新的一筆為 true，其餘強制設為 false
                var keep = currents.First();
                foreach (var s in currents.Skip(1))
                {
                    s.IsCurrent = false;
                }
            }
            else if (currents.Count == 0)
            {
                // 若皆無，預設最新學期為目前學期
                all.OrderByDescending(s => s.StartDate).First().IsCurrent = true;
            }
        }
    }

    public async Task<IActionResult> OnPostDeleteSemesterAsync(int semesterId)
    {
        var semester = await _db.Semesters.FindAsync(semesterId);
        if (semester == null)
        {
            TempData["ErrorMessage"] = "找不到指定的學期。";
            return RedirectToPage();
        }

        if (semester.IsCurrent)
        {
            TempData["ErrorMessage"] = "目前進行中之學期不可刪除，請先將其他學期設為目前學期。";
            return RedirectToPage();
        }

        var hasCourses = await _db.CourseProgresses.AnyAsync(c => c.SemesterId == semesterId);
        if (hasCourses)
        {
            TempData["ErrorMessage"] = "該學期已關聯課程進度資料，不可直接刪除。";
            return RedirectToPage();
        }

        _db.Semesters.Remove(semester);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已成功刪除學期「{semester.Name}」。";
        return RedirectToPage();
    }

    public static string GetDefaultTabForCategory(string category) => category switch
    {
        FieldCategories.School => "tab-school",
        FieldCategories.Grade => "tab-grade",
        FieldCategories.Class => "tab-class",
        FieldCategories.Subject => "tab-subject",
        _ => "tab-school"
    };

    public async Task<IActionResult> OnPostCreateHolidayAsync(DateTime date, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "請輸入休假日名稱。";
            return RedirectToPage();
        }

        var holidayDate = DateOnly.FromDateTime(date);
        var exists = await _db.AcademicHolidays.AnyAsync(h => h.Date == holidayDate && h.IsActive);
        if (exists)
        {
            TempData["ErrorMessage"] = $"{holidayDate:yyyy-MM-dd} 已經設定為休假日。";
            return RedirectToPage();
        }

        _db.AcademicHolidays.Add(new AcademicHoliday
        {
            Date = holidayDate,
            Name = name.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已新增休假日：{holidayDate:yyyy-MM-dd} ({name.Trim()})";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteHolidayAsync(int id)
    {
        var holiday = await _db.AcademicHolidays.FindAsync(id);
        if (holiday != null)
        {
            holiday.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"已刪除休假日：{holiday.Date:yyyy-MM-dd} ({holiday.Name})";
        }
        return RedirectToPage();
    }

    public static string GetCategoryDisplayName(string category) => category switch
    {
        FieldCategories.School => "學校",
        FieldCategories.Grade => "年別",
        FieldCategories.Class => "班別",
        FieldCategories.Subject => "任教科目",
        _ => category
    };

    private async Task<AppUser?> GetCurrentAuthorizedUserAsync()
    {
        var idStr = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var userId))
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
        }
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;
        if (user.Role == UserRole.Admin || (user.Role == UserRole.Teacher && user.IsProxyAdmin))
        {
            return user;
        }
        return null;
    }
}
