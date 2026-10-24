using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;
using Xunit;

namespace PlatoTutoring.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request);
    }
}

public class LineAuthAndBindingTests
{
    [Fact]
    public void BuildAuthorizeUrl_WhenConfigured_GeneratesValidOAuthUrl()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LineLogin:ClientId"] = "1234567890",
            ["LineLogin:ClientSecret"] = "secret_test_abc"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var http = new HttpClient(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var authService = new LineAuthService(config, http, NullLogger<LineAuthService>.Instance);

        Assert.True(authService.IsLineLoginConfigured());
        var url = authService.BuildAuthorizeUrl("https://my-app.azurewebsites.net/LineAuth/Callback", "state123");

        Assert.Contains("https://access.line.me/oauth2/v2.1/authorize", url);
        Assert.Contains("client_id=1234567890", url);
        Assert.Contains("state=state123", url);
        Assert.Contains("scope=profile%20openid", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Fmy-app.azurewebsites.net%2FLineAuth%2FCallback", url);
    }

    [Fact]
    public void BuildAuthorizeUrl_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var http = new HttpClient(new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var authService = new LineAuthService(config, http, NullLogger<LineAuthService>.Instance);

        Assert.False(authService.IsLineLoginConfigured());
        Assert.Throws<InvalidOperationException>(() =>
            authService.BuildAuthorizeUrl("https://localhost/LineAuth/Callback", "state123"));
    }

    [Fact]
    public async Task ExchangeCodeForProfileAsync_WithValidMock_ReturnsUserProfile()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LineLogin:ClientId"] = "1234567890",
            ["LineLogin:ClientSecret"] = "secret_test_abc"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var handler = new MockHttpMessageHandler(async req =>
        {
            if (req.RequestUri!.ToString().Contains("/oauth2/v2.1/token"))
            {
                var tokenJson = JsonSerializer.Serialize(new
                {
                    access_token = "mock_access_token_123",
                    token_type = "Bearer",
                    expires_in = 2592000,
                    scope = "profile openid"
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tokenJson) };
            }
            if (req.RequestUri!.ToString().Contains("/v2/profile"))
            {
                Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                Assert.Equal("mock_access_token_123", req.Headers.Authorization?.Parameter);

                var profileJson = JsonSerializer.Serialize(new
                {
                    userId = "U_LINE_PARENT_REAL_999",
                    displayName = "林媽媽",
                    statusMessage = "有事請留言"
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(profileJson) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler);
        var authService = new LineAuthService(config, http, NullLogger<LineAuthService>.Instance);

        var profile = await authService.ExchangeCodeForProfileAsync("test_auth_code", "https://localhost/LineAuth/Callback");

        Assert.NotNull(profile);
        Assert.Equal("U_LINE_PARENT_REAL_999", profile!.UserId);
        Assert.Equal("林媽媽", profile.DisplayName);
    }

    [Fact]
    public async Task SendPushMessageAsync_WhenTokenConfigured_CallsLineApiSuccessfully()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["LineBot:ChannelAccessToken"] = "mock_channel_access_token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var called = false;
        var handler = new MockHttpMessageHandler(async req =>
        {
            if (req.RequestUri!.ToString().Contains("/v2/bot/message/push"))
            {
                called = true;
                Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
                Assert.Equal("mock_channel_access_token", req.Headers.Authorization?.Parameter);

                var body = await req.Content!.ReadAsStringAsync();
                Assert.Contains("U_TARGET_PARENT_123", body);
                Assert.Contains("作業未完成通知", body);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler);
        var authService = new LineAuthService(config, http, NullLogger<LineAuthService>.Instance);

        var (success, msg) = await authService.SendPushMessageAsync("U_TARGET_PARENT_123", "作業未完成通知");

        Assert.True(success, msg);
        Assert.True(called);
        Assert.Contains("成功", msg);
    }

    [Fact]
    public async Task ParentUnbindFlow_ClearsLineUserId()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var parent = new AppUser
        {
            Username = "parent_test",
            PasswordHash = "pw",
            DisplayName = "陳爸爸",
            Role = UserRole.Parent,
            LineUserId = "U_EXISTING_LINE_ID"
        };
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        // 模擬解除綁定
        parent.LineUserId = null;
        await db.SaveChangesAsync();

        var refreshed = await db.Users.FindAsync(parent.Id);
        Assert.Null(refreshed!.LineUserId);
    }
}
