using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

var (dbProvider, resolvedConnectionString, dbDescription) = ResolveDatabaseConfiguration(builder.Configuration);

builder.Services.AddSingleton(new DeploymentRuntimeInfo(dbProvider, dbDescription));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer":
            options.UseSqlServer(resolvedConnectionString, sql => sql.EnableRetryOnFailure(3));
            break;
        case "PostgreSql":
            options.UseNpgsql(resolvedConnectionString, npgsql => npgsql.EnableRetryOnFailure(3));
            break;
        default:
            options.UseSqlite(resolvedConnectionString);
            break;
    }
});

builder.Services.AddScoped<CreditLedgerService>();
builder.Services.AddScoped<LineNotificationService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AttachmentStorageService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "PlatoTutoring.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParentAccess", p => p.RequireRole(nameof(UserRole.Parent), nameof(UserRole.Admin)));
    options.AddPolicy("TeacherAccess", p => p.RequireRole(nameof(UserRole.Teacher), nameof(UserRole.Admin)));
    options.AddPolicy("AdminAccess", p => p.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var ledger = scope.ServiceProvider.GetRequiredService<CreditLedgerService>();
    var booking = scope.ServiceProvider.GetRequiredService<BookingService>();
    await DbInitializer.SeedAsync(db, ledger, booking);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Index");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");

// 受保護的私有匯款憑證檢視端點（支援 Azure Blob Storage 與本機私有目錄，僅限老師與管理員）
app.MapGet("/attachments/{id:int}", async (
    int id,
    ClaimsPrincipal user,
    AppDbContext db,
    AttachmentStorageService storage) =>
{
    if (user.Identity?.IsAuthenticated != true)
        return Results.Challenge();

    var role = user.FindFirstValue(ClaimTypes.Role);
    if (role != nameof(UserRole.Teacher) && role != nameof(UserRole.Admin))
        return Results.Forbid();

    var att = await db.RemittanceAttachments.FindAsync(id);
    if (att == null || att.IsDeleted)
        return Results.NotFound("找不到附件或已依 90 天保存政策清除。");

    var stream = await storage.OpenAttachmentStreamAsync(att);
    if (stream == null)
        return Results.NotFound("找不到實體或雲端 Blob 附件檔案。");

    return Results.File(stream, att.ContentType, att.OriginalFileName);
});

// 管理員匯出學生堂數 CSV 端點
app.MapGet("/admin/export/credits.csv", async (
    ClaimsPrincipal user,
    CreditLedgerService ledgerService) =>
{
    if (user.Identity?.IsAuthenticated != true)
        return Results.Challenge();

    var role = user.FindFirstValue(ClaimTypes.Role);
    if (role != nameof(UserRole.Admin))
        return Results.Forbid();

    var csv = await ledgerService.ExportStudentsCreditsCsvAsync();
    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    return Results.File(bytes, "text/csv; charset=utf-8", $"plato-student-credits-{DateTime.UtcNow:yyyyMMdd}.csv");
});

app.MapRazorPages();

app.Run();

static (string Provider, string ConnectionString, string Description) ResolveDatabaseConfiguration(IConfiguration config)
{
    string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    var explicitProvider = FirstNonEmpty(
        Environment.GetEnvironmentVariable("DB_PROVIDER"),
        config["Database:Provider"]);

    // 1. 優先讀取完整連線字串環境變數（相容 Azure Web App 與一般容器環境變數）
    var rawConn = FirstNonEmpty(
        Environment.GetEnvironmentVariable("DB_CONNECTION_STRING"),
        Environment.GetEnvironmentVariable("DATABASE_URL"),
        Environment.GetEnvironmentVariable("SQLAZURECONNSTR_DefaultConnection"),
        Environment.GetEnvironmentVariable("SQLCONNSTR_DefaultConnection"),
        Environment.GetEnvironmentVariable("POSTGRESQLCONNSTR_DefaultConnection"),
        Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DefaultConnection"),
        config.GetConnectionString("DefaultConnection"));

    // 2. 若未提供完整字串，支援由獨立環境變數 (DB_HOST / DB_NAME / DB_USER / DB_PASSWORD) 自動組裝
    var dbHost = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_HOST"), Environment.GetEnvironmentVariable("DB_SERVER"));
    var dbName = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_NAME"), Environment.GetEnvironmentVariable("DB_DATABASE")) ?? "plato_tutoring";
    var dbUser = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_USER"), Environment.GetEnvironmentVariable("DB_USERNAME"));
    var dbPass = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PASSWORD"), Environment.GetEnvironmentVariable("DB_PASS"));
    var dbPort = FirstNonEmpty(Environment.GetEnvironmentVariable("DB_PORT"));

    if (!string.IsNullOrWhiteSpace(dbHost) && !string.IsNullOrWhiteSpace(dbUser))
    {
        var isPg = string.Equals(explicitProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(explicitProvider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
                   dbPort == "5432";

        if (isPg)
        {
            var port = dbPort ?? "5432";
            var builtPg = $"Host={dbHost};Port={port};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode=Prefer;Trust Server Certificate=true";
            return ("PostgreSql", builtPg, $"PostgreSQL 外部資料庫 ({dbHost}:{port}/{dbName})");
        }
        else
        {
            var serverPart = string.IsNullOrWhiteSpace(dbPort) ? dbHost : $"{dbHost},{dbPort}";
            var builtSql = $"Server={serverPart};Initial Catalog={dbName};User ID={dbUser};Password={dbPass};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
            return ("SqlServer", builtSql, $"SQL Server / Azure SQL 外部資料庫 ({serverPart}/{dbName})");
        }
    }

    if (!string.IsNullOrWhiteSpace(rawConn))
    {
        var lower = rawConn.ToLowerInvariant();
        if (string.Equals(explicitProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitProvider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains("host=") || lower.StartsWith("postgres://") || lower.StartsWith("postgresql://"))
        {
            return ("PostgreSql", rawConn, "PostgreSQL 外部資料庫 (由環境變數連線字串載入)");
        }

        if (string.Equals(explicitProvider, "SqlServer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitProvider, "MsSql", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains("server=") || lower.Contains("initial catalog=") || lower.Contains("user id=") || lower.Contains("data source=tcp:"))
        {
            return ("SqlServer", rawConn, "SQL Server / Azure SQL 外部資料庫 (由環境變數連線字串載入)");
        }

        return ("Sqlite", rawConn, $"SQLite 資料庫 ({rawConn})");
    }

    var fallbackSqlite = "Data Source=App_Data/plato_tutoring.db";
    return ("Sqlite", fallbackSqlite, "本機預設 SQLite (未設定外部 DB 環境變數時自動啟用)");
}

public record DeploymentRuntimeInfo(string DatabaseProvider, string DatabaseDescription);

public partial class Program { }
