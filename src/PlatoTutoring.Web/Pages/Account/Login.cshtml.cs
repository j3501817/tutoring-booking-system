using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;

    public LoginModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public string Username { get; set; } = "parent.lin";

    [BindProperty]
    public string Password { get; set; } = "demo123";

    public List<AppUser> DemoUsers { get; set; } = new();

    public async Task OnGetAsync()
    {
        DemoUsers = await _db.Users.OrderBy(u => u.Id).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var trimmedUsername = Username.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == trimmedUsername);

        if (user == null || !PasswordHelper.VerifyPassword(Password, user.PasswordHash))
        {
            TempData["ErrorMessage"] = "帳號或密碼不正確。";
            DemoUsers = await _db.Users.OrderBy(u => u.Id).ToListAsync();
            return Page();
        }

        await SignInAppUserAsync(user);
        TempData["SuccessMessage"] = $"歡迎登入，{user.DisplayName}！";
        return RedirectToRoleHome(user.Role);
    }

    public async Task<IActionResult> OnPostQuickSwitchAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            TempData["ErrorMessage"] = "找不到指定的驗收切換帳號。";
            return RedirectToPage("/Account/Login");
        }

        await SignInAppUserAsync(user);
        TempData["SuccessMessage"] = $"已切換身分為：{user.DisplayName} ({user.Role})";
        return RedirectToRoleHome(user.Role);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "已成功登出。";
        return RedirectToPage("/Account/Login");
    }

    private async Task SignInAppUserAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private IActionResult RedirectToRoleHome(UserRole role)
    {
        return role switch
        {
            UserRole.Parent => RedirectToPage("/Parent/Index"),
            UserRole.Teacher => RedirectToPage("/Teacher/Index"),
            UserRole.Admin => RedirectToPage("/Admin/Index"),
            _ => RedirectToPage("/Index")
        };
    }
}
