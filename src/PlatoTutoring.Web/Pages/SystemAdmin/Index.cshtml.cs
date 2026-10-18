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
    public List<FieldOption> ClassOptions { get; set; } = new();
    public List<FieldOption> RelationshipOptions { get; set; } = new();
    public List<FieldOption> SubjectOptions { get; set; } = new();

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

    public async Task OnGetAsync()
    {
        AllOptions = await _db.FieldOptions
            .OrderBy(o => o.Category)
            .ThenBy(o => o.ParentValue)
            .ThenBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToListAsync();

        SchoolOptions = AllOptions.Where(o => o.Category == FieldCategories.School).ToList();
        GradeOptions = AllOptions.Where(o => o.Category == FieldCategories.Grade).ToList();
        ClassOptions = AllOptions.Where(o => o.Category == FieldCategories.Class).ToList();
        RelationshipOptions = AllOptions.Where(o => o.Category == FieldCategories.Relationship).ToList();
        SubjectOptions = AllOptions.Where(o => o.Category == FieldCategories.Subject).ToList();
    }

    public async Task<IActionResult> OnPostCreateOptionAsync(
        string category,
        string value,
        string? parentValue,
        int sortOrder = 1,
        bool autoCreateDefaultClasses = true)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "請填寫完整的欄位類別與選項名稱。";
            return RedirectToPage();
        }

        var trimmedCat = category.Trim();
        var trimmedVal = value.Trim();
        var trimmedParent = string.IsNullOrWhiteSpace(parentValue) ? null : parentValue.Trim();

        if (trimmedCat == FieldCategories.Class && string.IsNullOrWhiteSpace(trimmedParent))
        {
            TempData["ErrorMessage"] = "新增「班級」選項時，請選擇所屬連動學校。";
            return RedirectToPage();
        }

        var exists = await _db.FieldOptions.AnyAsync(o =>
            o.Category == trimmedCat &&
            o.Value == trimmedVal &&
            o.ParentValue == trimmedParent);

        if (exists)
        {
            TempData["ErrorMessage"] = $"該類別下已存在相同的選項「{trimmedVal}」。";
            return RedirectToPage();
        }

        if (sortOrder <= 0)
        {
            var maxSort = await _db.FieldOptions
                .Where(o => o.Category == trimmedCat && o.ParentValue == trimmedParent)
                .Select(o => (int?)o.SortOrder)
                .MaxAsync() ?? 0;
            sortOrder = maxSort + 1;
        }

        _db.FieldOptions.Add(new FieldOption
        {
            Category = trimmedCat,
            Value = trimmedVal,
            ParentValue = trimmedParent,
            SortOrder = sortOrder,
            IsActive = true
        });

        // 若新增的是學校且勾選自動建立預設連動班級 (A班、B班、C班)
        if (trimmedCat == FieldCategories.School && autoCreateDefaultClasses)
        {
            var defaultClasses = new[] { "A班", "B班", "C班" };
            for (var i = 0; i < defaultClasses.Length; i++)
            {
                var cls = defaultClasses[i];
                var clsExists = await _db.FieldOptions.AnyAsync(o =>
                    o.Category == FieldCategories.Class &&
                    o.ParentValue == trimmedVal &&
                    o.Value == cls);
                if (!clsExists)
                {
                    _db.FieldOptions.Add(new FieldOption
                    {
                        Category = FieldCategories.Class,
                        Value = cls,
                        ParentValue = trimmedVal,
                        SortOrder = i + 1,
                        IsActive = true
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已新增欄位選項：{GetCategoryDisplayName(trimmedCat)} － {trimmedVal}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateOptionAsync(
        int optionId,
        string value,
        string? parentValue,
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

        var oldVal = opt.Value;
        var trimmedVal = value.Trim();
        var trimmedParent = string.IsNullOrWhiteSpace(parentValue) ? null : parentValue.Trim();

        opt.Value = trimmedVal;
        if (opt.Category == FieldCategories.Class)
        {
            opt.ParentValue = trimmedParent;
        }
        opt.SortOrder = sortOrder > 0 ? sortOrder : 1;

        // 若修改了學校名稱，同步更新該學校連動的班級 ParentValue
        if (opt.Category == FieldCategories.School && !string.Equals(oldVal, trimmedVal, StringComparison.Ordinal))
        {
            var linkedClasses = await _db.FieldOptions
                .Where(o => o.Category == FieldCategories.Class && o.ParentValue == oldVal)
                .ToListAsync();
            foreach (var cls in linkedClasses)
            {
                cls.ParentValue = trimmedVal;
            }
        }

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

        // 若刪除學校，一併刪除其連動的班級選項
        if (opt.Category == FieldCategories.School)
        {
            var linkedClasses = await _db.FieldOptions
                .Where(o => o.Category == FieldCategories.Class && o.ParentValue == opt.Value)
                .ToListAsync();
            if (linkedClasses.Count > 0)
            {
                _db.FieldOptions.RemoveRange(linkedClasses);
            }
        }

        _db.FieldOptions.Remove(opt);
        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已刪除欄位選項：{label}";
        return RedirectToPage();
    }

    public static string GetCategoryDisplayName(string category) => category switch
    {
        FieldCategories.School => "學校",
        FieldCategories.Grade => "年別",
        FieldCategories.Class => "班級（連動學校）",
        FieldCategories.Relationship => "關係稱謂",
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
