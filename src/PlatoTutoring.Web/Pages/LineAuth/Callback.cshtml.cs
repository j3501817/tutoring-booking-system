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
public class CallbackModel : PageModel
{
    private readonly LineAuthService _lineAuth;
    private readonly AppDbContext _db;
    private readonly ILogger<CallbackModel> _logger;

    public CallbackModel(LineAuthService lineAuth, AppDbContext db, ILogger<CallbackModel> logger)
    {
        _lineAuth = lineAuth;
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(
        string? code,
        string? state,
        string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        var cookie = Request.Cookies["Plato_LineState"];
        Response.Cookies.Delete("Plato_LineState");

        var returnUrl = "/Parent";
        string? expectedState = null;

        if (!string.IsNullOrWhiteSpace(cookie))
        {
            var parts = cookie.Split('|', 2);
            expectedState = parts[0];
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                returnUrl = parts[1];
            }
        }

        // 1. 檢查使用者是否在 LINE 點擊了拒絕或發生錯誤
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogWarning("LINE 授權被使用者拒絕或失敗: {Error} - {Desc}", error, errorDescription);
            TempData["ErrorMessage"] = $"LINE 授權未完成或已取消（{errorDescription ?? error}）。";
            return Redirect(returnUrl);
        }

        // 2. 驗證 CSRF State
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(expectedState) || state != expectedState)
        {
            _logger.LogWarning("LINE 授權 State 不符: 收到 {State}, 預期 {Expected}", state, expectedState);
            TempData["ErrorMessage"] = "LINE 授權驗證逾時或狀態不符，請重新點擊綁定。";
            return Redirect(returnUrl);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["ErrorMessage"] = "未收到 LINE 授權代碼 (Authorization Code)。";
            return Redirect(returnUrl);
        }

        // 3. 取得目前登入使用者
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

        // 4. 呼叫 LINE API 交換 Token 與個人資料
        try
        {
            var redirectUri = Url.Page("/LineAuth/Callback", null, null, Request.Scheme)
                              ?? $"{Request.Scheme}://{Request.Host}/LineAuth/Callback";

            var profile = await _lineAuth.ExchangeCodeForProfileAsync(code, redirectUri);
            if (profile == null || string.IsNullOrWhiteSpace(profile.UserId))
            {
                TempData["ErrorMessage"] = "LINE 授權成功但無法取得您的 LINE User ID，請重新嘗試。";
                return Redirect(returnUrl);
            }

            user.LineUserId = profile.UserId.Trim();
            await _db.SaveChangesAsync();

            _logger.LogInformation("使用者 {UserId} ({DisplayName}) 成功綁定 LINE: {LineUserId}",
                user.Id, user.DisplayName, user.LineUserId);

            TempData["SuccessMessage"] = $"【LINE 官方帳號通知綁定成功！】已成功連結 LINE「{profile.DisplayName}」，未來重要通知將即時推播至您的 LINE。";
            return Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理 LINE Callback 發生例外");
            TempData["ErrorMessage"] = $"LINE 授權綁定失敗：{ex.Message}";
            return Redirect(returnUrl);
        }
    }
}
