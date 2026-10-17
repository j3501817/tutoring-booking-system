using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public string CurrentSemesterName { get; set; } = string.Empty;
    public string CurrentProgressTitle { get; set; } = string.Empty;
    public int ActiveStudentCount { get; set; }
    public int TotalSystemCredits { get; set; }
    public int ConfirmedBookingCount { get; set; }
    public int ActiveWaitlistCount { get; set; }
    public int LineSentCount { get; set; }

    public async Task OnGetAsync()
    {
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
}
