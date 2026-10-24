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

    public bool IsLoginConfigured { get; set; }
    public bool IsMessagingConfigured { get; set; }
    public bool HasSecret { get; set; }
    public string? ReturnUrl { get; set; }
    public string? DetectedClientId { get; set; }
    public string? CurrentUserLineId { get; set; }
    public string? CurrentUserDisplayName { get; set; }
    public bool IsAdmin { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Parent" : returnUrl;
        IsLoginConfigured = _lineAuth.IsLineLoginConfigured(_db);
        IsMessagingConfigured = _lineAuth.IsMessagingConfigured();
        HasSecret = !string.IsNullOrWhiteSpace(_lineAuth.GetLoginClientSecret(_db));
        DetectedClientId = _lineAuth.GetLoginClientId(_db);

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out var userId))
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                CurrentUserLineId = user.LineUserId;
                CurrentUserDisplayName = user.DisplayName;
                IsAdmin = user.Role == UserRole.Admin;
            }
        }

        // 若已完整配置 LINE Login ClientId 與 Secret，直接跳轉 LINE 官方授權
        if (IsLoginConfigured)
        {
            try
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

                var authorizeUrl = _lineAuth.BuildAuthorizeUrl(redirectUri, state, _db);
                return Redirect(authorizeUrl);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                IsLoginConfigured = false;
            }
        }

        // 若尚未設定 LINE Login ClientId 或授權建置失敗，渲染引導說明頁面與提供直接/手動綁定
        return Page();
    }

    /// <summary>
    /// 家長直接手動輸入 LINE User ID 綁定並測試推播
    /// </summary>
    public async Task<IActionResult> OnPostDirectBindAsync(string lineUserId, string? returnUrl = null)
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

        if (string.IsNullOrWhiteSpace(lineUserId))
        {
            TempData["ErrorMessage"] = "請輸入有效的 LINE User ID。";
            return RedirectToPage(new { returnUrl = targetUrl });
        }

        user.LineUserId = lineUserId.Trim();
        await _db.SaveChangesAsync();

        var testNotice = "";
        if (_lineAuth.IsMessagingConfigured())
        {
            var (success, msg) = await _lineAuth.SendPushMessageAsync(
                user.LineUserId,
                $"【柏拉圖輔導預約系統】通知測試：恭喜 {user.DisplayName}！您的 LINE 官方帳號已完成綁定，後續將為您即時發送堂數異動、課堂提醒與預約通知。");
            testNotice = success ? "（系統已同步向您的 LINE 發送測試通知確認）" : $"（推播嘗試發送：{msg}）";
        }

        TempData["SuccessMessage"] = $"已成功綁定 LINE 官方帳號通知！{testNotice}";
        return Redirect(targetUrl);
    }

    /// <summary>
    /// 管理員可直接在畫面填入 LINE Channel ID (Client ID) 儲存至系統設定，免重啟伺服器
    /// </summary>
    public async Task<IActionResult> OnPostSaveChannelIdAsync(string channelId, string? returnUrl = null)
    {
        var targetUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Parent" : returnUrl;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            TempData["ErrorMessage"] = "請輸入 LINE Channel ID。";
            return RedirectToPage(new { returnUrl = targetUrl });
        }

        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "LineBot:ChannelId");
        if (setting == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "LineBot:ChannelId",
                Value = channelId.Trim(),
                Description = "LINE Login Channel ID / Client ID"
            });
        }
        else
        {
            setting.Value = channelId.Trim();
        }

        await _db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"已儲存 LINE Channel ID ({channelId.Trim()})！請重新點擊授權綁定即可完成一鍵跳轉。";
        return RedirectToPage(new { returnUrl = targetUrl });
    }

    /// <summary>
    /// 模擬/測試綁定
    /// </summary>
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

        TempData["SuccessMessage"] = $"[快速綁定成功] 已設定通知識別碼：{mockLineUserId}。";
        return Redirect(targetUrl);
    }
}
