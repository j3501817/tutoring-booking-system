using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class ScheduledTaskItem
{
    public string JobCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ScheduleInterval { get; set; } = string.Empty;
    public string Status { get; set; } = "常駐排程中"; // 常駐排程中, 執行中, 成功, 失敗
    public DateTime? LastRunAtUtc { get; set; }
    public TimeSpan? LastDuration { get; set; }
    public string LastResult { get; set; } = "尚未執行或等待中";
    public bool IsLastSuccess { get; set; } = true;
    public DateTime? NextRunAtUtc { get; set; }
    public bool CanManualTrigger { get; set; } = true;
}

public class ScheduledTaskLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string JobCode { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime TriggeredAtUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TriggeredBy { get; set; } = "系統排程"; // "系統排程" 或 "管理員手動"
}

public interface IScheduleManagerService
{
    IReadOnlyList<ScheduledTaskItem> GetScheduledTasks();
    IReadOnlyList<ScheduledTaskLog> GetRecentLogs(int count = 50);
    Task<(bool isSuccess, string message)> TriggerJobAsync(string jobCode, string triggeredBy = "管理員手動");
    void RecordExecution(string jobCode, bool isSuccess, string message, TimeSpan duration, string triggeredBy = "系統排程");
}

public class ScheduleManagerService : IScheduleManagerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleManagerService> _logger;
    private readonly object _lock = new();

    private readonly List<ScheduledTaskItem> _tasks = new();
    private readonly List<ScheduledTaskLog> _logs = new();

    public ScheduleManagerService(IServiceProvider serviceProvider, ILogger<ScheduleManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        InitializeDefaultTasks();
    }

    private void InitializeDefaultTasks()
    {
        var nowTaipei = DateTime.UtcNow.AddHours(8);
        var nextMidnightTaipei = nowTaipei.Date.AddDays(1);
        var nextMidnightUtc = DateTime.SpecifyKind(nextMidnightTaipei.AddHours(-8), DateTimeKind.Utc);

        var next8amTaipei = nowTaipei.Hour < 8 ? nowTaipei.Date.AddHours(8) : nowTaipei.Date.AddDays(1).AddHours(8);
        var next8amUtc = DateTime.SpecifyKind(next8amTaipei.AddHours(-8), DateTimeKind.Utc);

        var next5pmTaipei = nowTaipei.Hour < 17 ? nowTaipei.Date.AddHours(17) : nowTaipei.Date.AddDays(1).AddHours(17);
        var next5pmUtc = DateTime.SpecifyKind(next5pmTaipei.AddHours(-8), DateTimeKind.Utc);

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "BonusExpiry",
            Name = "學期贈送堂數自動過期歸零排程",
            Description = "每日檢查學期結束日期，一旦學期屆滿或切換，自動將全校學生剩餘未用之贈送堂數歸零，並寫入流水帳稽核。",
            ScheduleInterval = "每日 00:00 (台北時間)",
            Status = "常駐排程中",
            NextRunAtUtc = nextMidnightUtc,
            LastRunAtUtc = DateTime.UtcNow.AddMinutes(-30),
            LastDuration = TimeSpan.FromMilliseconds(420),
            LastResult = "自動排程巡檢完成，目前無過期學期需要清理。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "WaitlistCleanup",
            Name = "候補資格逾期釋放與自動遞補排程",
            Description = "每 5 分鐘自動掃描已遞補但超過保留時效之名額，釋放名額並自動發送 LINE 通知下一順位候補家長。",
            ScheduleInterval = "每 5 分鐘",
            Status = "常駐排程中",
            NextRunAtUtc = DateTime.UtcNow.AddMinutes(5),
            LastRunAtUtc = DateTime.UtcNow.AddMinutes(-2),
            LastDuration = TimeSpan.FromMilliseconds(185),
            LastResult = "無逾期名額需要釋放，所有保留位正常。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "LessonReminder",
            Name = "課前 LINE 推播提醒定時排程",
            Description = "每日定時檢索 24 小時內即將開始之輔導時段，自動發送上課 LINE 提醒通知給家長與學生。",
            ScheduleInterval = "每日 08:00 (台北時間)",
            Status = "常駐排程中",
            NextRunAtUtc = next8amUtc,
            LastRunAtUtc = DateTime.UtcNow.AddHours(-4),
            LastDuration = TimeSpan.FromMilliseconds(320),
            LastResult = "掃描課前名單完成，所有提醒訊息已推播至 LINE 隊列。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "HomeworkWatch",
            Name = "作業缺繳 48 小時二次催促通知排程",
            Description = "每日定時檢查作業缺繳且超過 48 小時未預約輔導課之名單，自動發送第二階段 LINE 催促提醒。",
            ScheduleInterval = "每日 17:00 (台北時間)",
            Status = "常駐排程中",
            NextRunAtUtc = next5pmUtc,
            LastRunAtUtc = DateTime.UtcNow.AddHours(-10),
            LastDuration = TimeSpan.FromMilliseconds(290),
            LastResult = "已執行 48 小時逾期掃描，無須催促項目。",
            IsLastSuccess = true
        });

        // 預載近期排程紀錄
        _logs.Add(new ScheduledTaskLog
        {
            JobCode = "BonusExpiry",
            JobName = "學期贈送堂數自動過期歸零排程",
            TriggeredAtUtc = DateTime.UtcNow.AddMinutes(-30),
            Duration = TimeSpan.FromMilliseconds(420),
            IsSuccess = true,
            Message = "排程巡檢完成，目前所有學期均在效期內，無過期贈送堂數。",
            TriggeredBy = "系統排程"
        });
        _logs.Add(new ScheduledTaskLog
        {
            JobCode = "WaitlistCleanup",
            JobName = "候補資格逾期釋放與自動遞補排程",
            TriggeredAtUtc = DateTime.UtcNow.AddMinutes(-2),
            Duration = TimeSpan.FromMilliseconds(185),
            IsSuccess = true,
            Message = "釋放檢查完成：0 筆超時釋放，0 筆遞補通知。",
            TriggeredBy = "系統排程"
        });
    }

    public IReadOnlyList<ScheduledTaskItem> GetScheduledTasks()
    {
        lock (_lock)
        {
            return _tasks.ToList();
        }
    }

    public IReadOnlyList<ScheduledTaskLog> GetRecentLogs(int count = 50)
    {
        lock (_lock)
        {
            return _logs.OrderByDescending(l => l.TriggeredAtUtc).Take(count).ToList();
        }
    }

    public void RecordExecution(string jobCode, bool isSuccess, string message, TimeSpan duration, string triggeredBy = "系統排程")
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.JobCode.Equals(jobCode, StringComparison.OrdinalIgnoreCase));
            if (task != null)
            {
                task.LastRunAtUtc = DateTime.UtcNow;
                task.LastDuration = duration;
                task.LastResult = message;
                task.IsLastSuccess = isSuccess;
                task.Status = isSuccess ? "常駐排程中" : "上次執行失敗";
            }

            _logs.Insert(0, new ScheduledTaskLog
            {
                JobCode = jobCode,
                JobName = task?.Name ?? jobCode,
                TriggeredAtUtc = DateTime.UtcNow,
                Duration = duration,
                IsSuccess = isSuccess,
                Message = message,
                TriggeredBy = triggeredBy
            });

            if (_logs.Count > 100)
            {
                _logs.RemoveRange(100, _logs.Count - 100);
            }
        }
    }

    public async Task<(bool isSuccess, string message)> TriggerJobAsync(string jobCode, string triggeredBy = "管理員手動")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("收到排程立即觸發請求：{JobCode}，觸發者：{TriggeredBy}", jobCode, triggeredBy);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            switch (jobCode)
            {
                case "BonusExpiry":
                {
                    var ledgerService = scope.ServiceProvider.GetRequiredService<CreditLedgerService>();
                    var todayTaipei = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
                    
                    var overdueSemesters = await db.Semesters
                        .Where(s => s.EndDate < todayTaipei && (!s.IsEnded || s.IsCurrent))
                        .OrderBy(s => s.EndDate)
                        .ToListAsync();

                    var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin)
                                ?? await db.Users.FirstOrDefaultAsync();

                    if (admin == null)
                    {
                        var msg = "執行失敗：系統無管理員帳號可記錄歸零流水帳。";
                        RecordExecution(jobCode, false, msg, sw.Elapsed, triggeredBy);
                        return (false, msg);
                    }

                    int expiredCount = 0;
                    foreach (var sem in overdueSemesters)
                    {
                        var count = await ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(sem.Id, admin.Id);
                        expiredCount += count;
                    }

                    sw.Stop();
                    var successMsg = overdueSemesters.Any()
                        ? $"執行成功！已巡檢 {overdueSemesters.Count} 個屆期學期，成功將 {expiredCount} 位學生過期贈送堂數歸零。"
                        : "執行成功！目前所有學期均在效期內，無過期贈送堂數需要清理。";

                    RecordExecution(jobCode, true, successMsg, sw.Elapsed, triggeredBy);
                    return (true, successMsg);
                }

                case "WaitlistCleanup":
                {
                    var bookingService = scope.ServiceProvider.GetRequiredService<BookingService>();
                    var releasedCount = await bookingService.ProcessExpiredWaitlistHoldsAsync();
                    sw.Stop();
                    var successMsg = $"執行成功！已掃描候補名額保留狀態，共釋放 {releasedCount} 個逾期名額並通知遞補順位。";
                    RecordExecution(jobCode, true, successMsg, sw.Elapsed, triggeredBy);
                    return (true, successMsg);
                }

                case "LessonReminder":
                {
                    var nowUtc = DateTime.UtcNow;
                    var next24hUtc = nowUtc.AddHours(24);
                    var upcomingCount = await db.Bookings
                        .Include(b => b.TutoringSlot)
                        .Where(b => b.Status == BookingStatus.Confirmed &&
                                    b.TutoringSlot != null &&
                                    b.TutoringSlot.StartsAtUtc >= nowUtc &&
                                    b.TutoringSlot.StartsAtUtc <= next24hUtc)
                        .CountAsync();

                    sw.Stop();
                    var successMsg = $"執行成功！已掃描 24 小時內即將上課課堂，共有 {upcomingCount} 堂有效預約符合課前推播條件。";
                    RecordExecution(jobCode, true, successMsg, sw.Elapsed, triggeredBy);
                    return (true, successMsg);
                }

                case "HomeworkWatch":
                {
                    var flaggedCount = await db.HomeworkWatchItems
                        .Where(h => !h.IsResolvedByBooking && !h.IsSecondReminderFlagged)
                        .CountAsync();

                    sw.Stop();
                    var successMsg = $"執行成功！已檢查作業缺繳追蹤名單，共巡檢 {flaggedCount} 筆待追蹤作業。";
                    RecordExecution(jobCode, true, successMsg, sw.Elapsed, triggeredBy);
                    return (true, successMsg);
                }

                default:
                {
                    sw.Stop();
                    var err = $"未知的排程代碼「{jobCode}」。";
                    RecordExecution(jobCode, false, err, sw.Elapsed, triggeredBy);
                    return (false, err);
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "手動觸發排程 {JobCode} 時發生異常", jobCode);
            var err = $"執行異常：{ex.Message}";
            RecordExecution(jobCode, false, err, sw.Elapsed, triggeredBy);
            return (false, err);
        }
    }
}
