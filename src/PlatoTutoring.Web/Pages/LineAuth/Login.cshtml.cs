using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages.LineAuth;

[Authorize]
public class LoginModel : PageModel
{
    private readonly LineAuthService _lineAuth;
    private readonly AppDbContext _db;

    public LoginModel(LineAuthService lineAuth, AppDbContext db)
    {
        _lineAuth = lineAuth;
        _db = db;
    }

    public bool IsConfigured { get; set; }
    public string? ReturnUrl { get; set; }
    public string? DetectedClientId { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Parent" : returnUrl;
        IsConfigured = _lineAuth.IsLineLoginConfigured();
        DetectedClientId = _lineAuth.GetLoginClientId();

        if (IsConfigured)
        {
            var state = Guid.NewGuid().ToString("N");
            var statePayload = $"{state}|{ReturnUrl}";

            Response.Cookies.Append("Plato_LineState", statePayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            var redirectUri = Url.Page("/LineAuth/Callback", null, null, Request.Scheme)
                              ?? $"{Request.Scheme}://{Request.Host}/LineAuth/Callback";

            var authorizeUrl = _lineAuth.BuildAuthorizeUrl(redirectUri, state);
            return Redirect(authorizeUrl);
        }

        // 尚未設定 LINE Login 金鑰時，渲染引導說明頁面與提供本機測試模擬綁定
        return Page();
    }

    // 提供未填寫金鑰時的模擬/測試綁定（利於本機開發與單元測試）
    public async Task<IActionResult> OnPostSimulateBindAsync(string? returnUrl = null)
    {
        var targetUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Parent" : returnUrl;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return RedirectToPage("/Account/Login");
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        var mockLineUserId = $"U_MOCK_{user.Id:D4}_{DateTime.UtcNow:mmss}";
        user.LineUserId = mockLineUserId;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"[測試模擬] 已成功綁定測試 LINE 帳號：{mockLineUserId}。正式環境請設定 LineLogin__ClientId 與 Secret。";
        return Redirect(targetUrl);
    }
}
