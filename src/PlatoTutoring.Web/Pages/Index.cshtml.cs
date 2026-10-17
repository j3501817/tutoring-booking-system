using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Pages;

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

    public string CurrentSemesterName { get; set; } = "載入中...";
    public string CurrentProgressTitle { get; set; } = "載入中...";
    public int ActiveStudentCount { get; set; }
    public int TotalSystemCredits { get; set; }
    public int ConfirmedBookingCount { get; set; }
    public int ActiveWaitlistCount { get; set; }
    public int LineSentCount { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            await DbInitializer.EnsureInitializedSafeAsync(_db, _ledgerService, _bookingService);

            var semester = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
            CurrentSemesterName = semester?.Name ?? "未設定學期";

            var progress = await _db.CourseProgresses.FirstOrDefaultAsync(p => p.IsCurrentProgress);
            CurrentProgressTitle = progress != null ? $"{progress.Code} {progress.Title}" : "未設定目前進度";

            var activeStudents = await _db.Students.Where(s => !s.IsDroppedOut).ToListAsync();
            ActiveStudentCount = activeStudents.Count;
            TotalSystemCredits = activeStudents.Sum(s => s.PaidCredits + s.BonusCredits);

            ConfirmedBookingCount = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed);
            ActiveWaitlistCount = await _db.WaitlistEntries.CountAsync(w =>
                w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.NotifiedHold);
            LineSentCount = await _db.LineNotificationLogs.CountAsync();
        }
        catch (Exception ex)
        {
            CurrentSemesterName = "資料庫連線待確認";
            CurrentProgressTitle = "請檢查 Azure SQL 防火牆或連線字串";
            TempData["ErrorMessage"] = $"資料庫連線或初始化提醒：{ex.Message}";
        }
    }
}
