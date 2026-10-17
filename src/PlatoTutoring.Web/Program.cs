using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=App_Data/plato_tutoring.db";

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

builder.Services.AddScoped<CreditLedgerService>();
builder.Services.AddScoped<LineNotificationService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AttachmentStorageService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParentAccess", p => p.RequireRole(nameof(UserRole.Parent), nameof(UserRole.Admin)));
    options.AddPolicy("TeacherAccess", p => p.RequireRole(nameof(UserRole.Teacher), nameof(UserRole.Admin)));
    options.AddPolicy("AdminAccess", p => p.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var ledger = scope.ServiceProvider.GetRequiredService<CreditLedgerService>();
    var booking = scope.ServiceProvider.GetRequiredService<BookingService>();
    await DbInitializer.SeedAsync(db, ledger, booking);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// 受保護的私有匯款憑證檢視端點（僅限老師與管理員，不對外公開）
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

    var fileInfo = storage.GetAttachmentFile(att);
    if (fileInfo == null)
        return Results.NotFound("找不到實體檔案。");

    return Results.File(fileInfo.Value.FullPath, att.ContentType, att.OriginalFileName);
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

public partial class Program { }
