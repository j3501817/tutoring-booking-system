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
    public List<FieldOption> AllOptions { get; set; } = new();
    public List<FieldOption> SchoolOptions { get; set; } = new();
    public List<FieldOption> GradeOptions { get; set; } = new();
    public List<FieldOption> ClassOptions { get; set; } = new(); // 班別
    public List<FieldOption> SubjectOptions { get; set; } = new();
    public List<AttendancePurposeOption> PurposeOptions { get; set; } = new(); // 到班目的
    public List<AcademicHoliday> AcademicHolidays { get; set; } = new(); // 休假日
    public Dictionary<DateOnly, AcademicHoliday> HolidayMap { get; set; } = new();
    public Semester? CurrentSemester { get; set; }

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

    public async Task OnGetAsync(int? year = null, int? month = null)
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

        CurrentSemester = await _db.Semesters
            .FirstOrDefaultAsync(s => s.IsCurrent)
            ?? await _db.Semesters.OrderByDescending(s => s.StartDate).FirstOrDefaultAsync();

        if (year.HasValue && month.HasValue && month.Value >= 1 && month.Value <= 12)
        {
            CalendarYear = year.Value;
            CalendarMonth = month.Value;
        }
        else
        {
            var today = DateTime.Today;
            CalendarYear = today.Year;
            CalendarMonth = today.Month;
        }

        DaysInMonth = DateTime.DaysInMonth(CalendarYear, CalendarMonth);
        StartDayOfWeek = (int)new DateOnly(CalendarYear, CalendarMonth, 1).DayOfWeek;

        var prev = new DateTime(CalendarYear, CalendarMonth, 1).AddMonths(-1);
        PrevYear = prev.Year;
        PrevMonth = prev.Month;

        var next = new DateTime(CalendarYear, CalendarMonth, 1).AddMonths(1);
        NextYear = next.Year;
        NextMonth = next.Month;
    }

    public async Task<IActionResult> OnPostCreateOptionAsync(
        string category,
        string value,
        int sortOrder = 1)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "請填寫完整的欄位類別與選項名稱。";
            return RedirectToPage();
        }

        var trimmedCat = category.Trim();
        var trimmedVal = value.Trim();

        var exists = await _db.FieldOptions.AnyAsync(o =>
            o.Category == trimmedCat &&
            o.Value == trimmedVal);

        if (exists)
        {
            TempData["ErrorMessage"] = $"該類別下已存在相同的選項「{trimmedVal}」。";
            return RedirectToPage();
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
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateOptionAsync(
        int optionId,
        string value,
        int sortOrder = 1)
    {
        var opt = await _db.FieldOptions.FindAsync(optionId);
        if (opt == null)
        {
            TempData["ErrorMessage"] = "找不到指定的欄位選項。";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "選項名稱不可為空白。";
            return RedirectToPage();
        }

        opt.Value = value.Trim();
        opt.ParentValue = null;
        opt.SortOrder = sortOrder > 0 ? sortOrder : 1;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已更新欄位選項：{GetCategoryDisplayName(opt.Category)} － {opt.Value}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteOptionAsync(int optionId)
    {
        var opt = await _db.FieldOptions.FindAsync(optionId);
        if (opt == null)
        {
            TempData["ErrorMessage"] = "找不到指定的欄位選項。";
            return RedirectToPage();
        }

        var label = $"{GetCategoryDisplayName(opt.Category)} － {opt.Value}";
        _db.FieldOptions.Remove(opt);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已刪除欄位選項：{label}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreatePurposeAsync(string name, string? description, int sortOrder = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "到班目的名稱不可為空白。";
            return RedirectToPage();
        }

        var trimmedName = name.Trim();
        var exists = await _db.AttendancePurposeOptions.AnyAsync(p => p.Name == trimmedName);
        if (exists)
        {
            TempData["ErrorMessage"] = $"已存在相同的到班目的「{trimmedName}」。";
            return RedirectToPage();
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
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdatePurposeAsync(int id, string name, int sortOrder = 1)
    {
        var purpose = await _db.AttendancePurposeOptions.FindAsync(id);
        if (purpose == null)
        {
            TempData["ErrorMessage"] = "找不到指定的到班目的。";
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "到班目的名稱不可為空白。";
            return RedirectToPage();
        }

        purpose.Name = name.Trim();
        purpose.SortOrder = sortOrder > 0 ? sortOrder : 1;

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已更新到班目的：{purpose.Name}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePurposeAsync(int id)
    {
        var purpose = await _db.AttendancePurposeOptions.FindAsync(id);
        if (purpose == null)
        {
            TempData["ErrorMessage"] = "找不到指定的到班目的。";
            return RedirectToPage();
        }

        var name = purpose.Name;
        _db.AttendancePurposeOptions.Remove(purpose);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已刪除到班目的：{name}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveSemesterAsync(string? name, DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            TempData["ErrorMessage"] = "學期結束日期必須晚於開始日期。";
            return RedirectToPage();
        }

        var startOnly = DateOnly.FromDateTime(startDate);
        var endOnly = DateOnly.FromDateTime(endDate);

        var semester = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent)
                       ?? await _db.Semesters.OrderByDescending(s => s.StartDate).FirstOrDefaultAsync();

        if (semester == null)
        {
            semester = new Semester
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"{startDate:yyyy} 學年度" : name.Trim(),
                StartDate = startOnly,
                EndDate = endOnly,
                IsCurrent = true
            };
            _db.Semesters.Add(semester);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name)) semester.Name = name.Trim();
            semester.StartDate = startOnly;
            semester.EndDate = endOnly;
            semester.IsCurrent = true;
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已儲存學期期間：{semester.StartDate:yyyy-MM-dd} ～ {semester.EndDate:yyyy-MM-dd}";
        return RedirectToPage();
    }

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
