using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlatoTutoring.Web.Data;

namespace PlatoTutoring.Web.Services;

public class LineUserProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }
}

public class LineTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

public class LineAuthService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ILogger<LineAuthService> _logger;

    public LineAuthService(IConfiguration config, HttpClient http, ILogger<LineAuthService> logger)
    {
        _config = config;
        _http = http;
        _logger = logger;
    }

    public string? GetLoginClientId(AppDbContext? db = null)
    {
        var val = _config["LineLogin:ClientId"]
            ?? _config["LineLogin__ClientId"]
            ?? Environment.GetEnvironmentVariable("LineLogin__ClientId")
            ?? Environment.GetEnvironmentVariable("LINE_LOGIN_CLIENT_ID")
            ?? _config["LineBot:ChannelId"]
            ?? _config["LineBot__ChannelId"]
            ?? Environment.GetEnvironmentVariable("LineBot__ChannelId")
            ?? Environment.GetEnvironmentVariable("LINE_CHANNEL_ID");

        if (string.IsNullOrWhiteSpace(val) && db != null)
        {
            try
            {
                val = db.SystemSettings.FirstOrDefault(s => s.Key == "LineLogin:ClientId" || s.Key == "LineBot:ChannelId")?.Value;
            }
            catch { }
        }

        return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
    }

    public string? GetLoginClientSecret(AppDbContext? db = null)
    {
        var val = _config["LineLogin:ClientSecret"]
            ?? _config["LineLogin__ClientSecret"]
            ?? Environment.GetEnvironmentVariable("LineLogin__ClientSecret")
            ?? Environment.GetEnvironmentVariable("LINE_LOGIN_CLIENT_SECRET")
            ?? _config["LineBot:ChannelSecret"]
            ?? _config["LineBot__ChannelSecret"]
            ?? Environment.GetEnvironmentVariable("LineBot__ChannelSecret")
            ?? Environment.GetEnvironmentVariable("LINE_CHANNEL_SECRET");

        if (string.IsNullOrWhiteSpace(val) && db != null)
        {
            try
            {
                val = db.SystemSettings.FirstOrDefault(s => s.Key == "LineLogin:ClientSecret" || s.Key == "LineBot:ChannelSecret")?.Value;
            }
            catch { }
        }

        return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
    }

    public string? GetChannelAccessToken()
    {
        return _config["LineBot:ChannelAccessToken"]
            ?? _config["LineBot__ChannelAccessToken"]
            ?? Environment.GetEnvironmentVariable("LineBot__ChannelAccessToken")
            ?? Environment.GetEnvironmentVariable("LINE_CHANNEL_ACCESS_TOKEN");
    }

    public bool IsMessagingConfigured()
    {
        return !string.IsNullOrWhiteSpace(GetChannelAccessToken());
    }

    public bool IsLineLoginConfigured(AppDbContext? db = null)
    {
        var clientId = GetLoginClientId(db);
        var clientSecret = GetLoginClientSecret(db);
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var clientId = GetLoginClientId();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("尚未設定 LINE Login Client ID (請在環境變數或 appsettings.json 設定 LineLogin__ClientId 或 LineBot__ChannelId)。");
        }

        var encodedRedirect = Uri.EscapeDataString(redirectUri);
        var encodedState = Uri.EscapeDataString(state);

        return $"https://access.line.me/oauth2/v2.1/authorize?response_type=code&client_id={clientId}&redirect_uri={encodedRedirect}&state={encodedState}&scope=profile%20openid&prompt=consent";
    }

    public async Task<LineUserProfile?> ExchangeCodeForProfileAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var clientId = GetLoginClientId();
        var clientSecret = GetLoginClientSecret();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("尚未設定完整的 LINE Login Client ID 或 Client Secret。");
        }

        // 1. 拿 authorization_code 換取 access_token
        var tokenUrl = "https://api.line.me/oauth2/v2.1/token";
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        var tokenRes = await _http.SendAsync(tokenReq, ct);
        var tokenBody = await tokenRes.Content.ReadAsStringAsync(ct);

        if (!tokenRes.IsSuccessStatusCode)
        {
            _logger.LogError("LINE Token 交換失敗: {Status} - {Body}", tokenRes.StatusCode, tokenBody);
            throw new InvalidOperationException($"LINE 授權交換失敗 (HTTP {(int)tokenRes.StatusCode})：{tokenBody}");
        }

        var tokenData = JsonSerializer.Deserialize<LineTokenResponse>(tokenBody);
        if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.AccessToken))
        {
            throw new InvalidOperationException("LINE Token 回傳格式錯誤，未取得 access_token。");
        }

        // 2. 呼叫 /v2/profile 取得使用者的真實 LINE User ID 與顯示名稱
        var profileUrl = "https://api.line.me/v2/profile";
        using var profileReq = new HttpRequestMessage(HttpMethod.Get, profileUrl);
        profileReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenData.AccessToken);

        var profileRes = await _http.SendAsync(profileReq, ct);
        var profileBody = await profileRes.Content.ReadAsStringAsync(ct);

        if (!profileRes.IsSuccessStatusCode)
        {
            _logger.LogError("LINE Profile 取得失敗: {Status} - {Body}", profileRes.StatusCode, profileBody);
            throw new InvalidOperationException($"LINE 個人資料取得失敗 (HTTP {(int)profileRes.StatusCode})：{profileBody}");
        }

        var profile = JsonSerializer.Deserialize<LineUserProfile>(profileBody);
        return profile;
    }

    public async Task<(bool Success, string Message)> SendPushMessageAsync(string lineUserId, string messageText, CancellationToken ct = default)
    {
        var token = GetChannelAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "尚未設定 LineBot:ChannelAccessToken，略過真實 API 發送。");
        }

        if (string.IsNullOrWhiteSpace(lineUserId) || lineUserId == "UNBOUND")
        {
            return (false, "家長未綁定 LINE User ID。");
        }

        var pushUrl = "https://api.line.me/v2/bot/message/push";
        var payload = new
        {
            to = lineUserId,
            messages = new[]
            {
                new { type = "text", text = messageText }
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, pushUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, jsonOptions), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (res.IsSuccessStatusCode)
            {
                return (true, "LINE 訊息推播成功");
            }
            _logger.LogWarning("LINE Push 失敗: {Status} - {Body}", res.StatusCode, body);
            return (false, $"LINE API 回傳錯誤 (HTTP {(int)res.StatusCode}): {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "呼叫 LINE Push API 發生網路例外");
            return (false, $"呼叫 LINE 發生例外：{ex.Message}");
        }
    }
}
