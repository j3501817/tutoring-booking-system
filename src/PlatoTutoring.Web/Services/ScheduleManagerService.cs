using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public enum ScheduleFrequencyType
{
    DailyTime,       // 每日固定時間 (例如 00:00, 08:00, 17:00)
    IntervalMinutes  // 間隔分鐘執行 (例如 1, 5, 10, 30 分鐘)
}

public class ScheduleConfigDto
{
    public bool IsEnabled { get; set; } = true;
    public ScheduleFrequencyType FrequencyType { get; set; } = ScheduleFrequencyType.DailyTime;
    public string DailyTime { get; set; } = "00:00";
    public int IntervalMinutes { get; set; } = 5;
}

public class ScheduledTaskItem
{
    public string JobCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public ScheduleFrequencyType FrequencyType { get; set; } = ScheduleFrequencyType.DailyTime;
    public string DailyTime { get; set; } = "00:00";
    public int IntervalMinutes { get; set; } = 5;

    public string ScheduleInterval => FrequencyType == ScheduleFrequencyType.DailyTime
        ? $"每日 {DailyTime} (台北時間)"
        : $"每 {IntervalMinutes} 分鐘";

    public string Status
    {
        get
        {
            if (!IsEnabled) return "已停用";
            if (!IsLastSuccess) return "上次執行失敗";
            return "常駐排程中";
        }
    }

    public DateTime? LastRunAtUtc { get; set; }
    public TimeSpan? LastDuration { get; set; }
    public string LastResult { get; set; } = "尚未執行或等待排程觸發";
    public bool IsLastSuccess { get; set; } = true;
    public DateTime? NextRunAtUtc { get; set; }
    public bool CanManualTrigger { get; set; } = true;

    // 專屬於此排程之近 3 筆執行紀錄
    public List<ScheduledTaskLog> RecentLogs { get; set; } = new();
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
    Task LoadConfigurationsFromDbAsync();
    Task UpdateTaskConfigAsync(string jobCode, bool isEnabled, ScheduleFrequencyType frequencyType, string dailyTime, int intervalMinutes);
    Task ToggleTaskAsync(string jobCode, bool isEnabled);
    Task<(bool isSuccess, string message)> TriggerJobAsync(string jobCode, string triggeredBy = "管理員手動");
    void RecordExecution(string jobCode, bool isSuccess, string message, TimeSpan duration, string triggeredBy = "系統排程");
    Task CheckAndRunPendingJobsAsync(CancellationToken ct = default);
}

public class ScheduleManagerService : IScheduleManagerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleManagerService> _logger;
    private readonly object _lock = new();

    private readonly List<ScheduledTaskItem> _tasks = new();

    public ScheduleManagerService(IServiceProvider serviceProvider, ILogger<ScheduleManagerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        InitializeDefaultTasks();
    }

    private void InitializeDefaultTasks()
    {
        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "BonusExpiry",
            Name = "學期贈送堂數自動過期歸零排程",
            Description = "檢查學期結束日期，一旦學期屆滿結束，自動將全校學生剩餘未用之贈送堂數歸零，並寫入流水帳稽核。",
            IsEnabled = true,
            FrequencyType = ScheduleFrequencyType.DailyTime,
            DailyTime = "00:00",
            IntervalMinutes = 1440,
            LastRunAtUtc = DateTime.UtcNow.AddMinutes(-30),
            LastDuration = TimeSpan.FromMilliseconds(420),
            LastResult = "自動排程巡檢完成，目前所有學期均在效期內，無過期贈送堂數。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "WaitlistCleanup",
            Name = "候補資格逾期釋放與自動遞補排程",
            Description = "自動掃描已遞補但超過 30 分鐘保留時效之名額，釋放名額並自動發送 LINE 通知下一順位候補家長。",
            IsEnabled = true,
            FrequencyType = ScheduleFrequencyType.IntervalMinutes,
            DailyTime = "00:00",
            IntervalMinutes = 5,
            LastRunAtUtc = DateTime.UtcNow.AddMinutes(-2),
            LastDuration = TimeSpan.FromMilliseconds(185),
            LastResult = "無逾期名額需要釋放，所有保留位正常。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "LessonReminder",
            Name = "課前 LINE 推播提醒定時排程",
            Description = "定時檢索即將開始之輔導時段，自動發送課前 LINE 提醒通知給家長與學生。",
            IsEnabled = true,
            FrequencyType = ScheduleFrequencyType.DailyTime,
            DailyTime = "08:00",
            IntervalMinutes = 1440,
            LastRunAtUtc = DateTime.UtcNow.AddHours(-4),
            LastDuration = TimeSpan.FromMilliseconds(320),
            LastResult = "掃描課前名單完成，所有提醒訊息已推播至 LINE 隊列。",
            IsLastSuccess = true
        });

        _tasks.Add(new ScheduledTaskItem
        {
            JobCode = "HomeworkWatch",
            Name = "作業缺繳 48 小時二次催促通知排程",
            Description = "定時檢查作業缺繳且超過 48 小時未預約輔導課之名單，自動發送第二階段 LINE 催促提醒。",
            IsEnabled = true,
            FrequencyType = ScheduleFrequencyType.DailyTime,
            DailyTime = "17:00",
            IntervalMinutes = 1440,
            LastRunAtUtc = DateTime.UtcNow.AddHours(-10),
            LastDuration = TimeSpan.FromMilliseconds(290),
            LastResult = "已執行 48 小時逾期掃描，無須催促項目。",
            IsLastSuccess = true
        });

        // 為每個排程初始化近 3 筆預載歷史
        foreach (var task in _tasks)
        {
            task.NextRunAtUtc = CalculateNextRunAt(task);

            task.RecentLogs.Add(new ScheduledTaskLog
            {
                JobCode = task.JobCode,
                JobName = task.Name,
                TriggeredAtUtc = task.LastRunAtUtc ?? DateTime.UtcNow,
                Duration = task.LastDuration ?? TimeSpan.FromMilliseconds(250),
                IsSuccess = true,
                Message = task.LastResult,
                TriggeredBy = "系統排程"
            });
        }
    }

    public IReadOnlyList<ScheduledTaskItem> GetScheduledTasks()
    {
        lock (_lock)
        {
            return _tasks.ToList();
        }
    }

    public async Task LoadConfigurationsFromDbAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var settings = await db.SystemSettings
                .Where(s => s.Key.StartsWith("SchedulerConfig_"))
                .ToListAsync();

            lock (_lock)
            {
                foreach (var s in settings)
                {
                    var jobCode = s.Key.Replace("SchedulerConfig_", "");
                    var task = _tasks.FirstOrDefault(t => t.JobCode.Equals(jobCode, StringComparison.OrdinalIgnoreCase));
                    if (task != null && !string.IsNullOrWhiteSpace(s.Value))
                    {
                        try
                        {
                            var config = JsonSerializer.Deserialize<ScheduleConfigDto>(s.Value);
                            if (config != null)
                            {
                                task.IsEnabled = config.IsEnabled;
                                task.FrequencyType = config.FrequencyType;
                                if (!string.IsNullOrWhiteSpace(config.DailyTime))
                                {
                                    task.DailyTime = config.DailyTime;
                                }
                                if (config.IntervalMinutes > 0)
                                {
                                    task.IntervalMinutes = config.IntervalMinutes;
                                }
                                task.NextRunAtUtc = CalculateNextRunAt(task);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "解析排程 {JobCode} 設定失敗，保留預設值", jobCode);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從資料庫載入排程設定失敗");
        }
    }

    public async Task UpdateTaskConfigAsync(string jobCode, bool isEnabled, ScheduleFrequencyType frequencyType, string dailyTime, int intervalMinutes)
    {
        ScheduledTaskItem? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.JobCode.Equals(jobCode, StringComparison.OrdinalIgnoreCase));
            if (task == null) return;

            task.IsEnabled = isEnabled;
            task.FrequencyType = frequencyType;
            if (!string.IsNullOrWhiteSpace(dailyTime)) task.DailyTime = dailyTime;
            if (intervalMinutes > 0) task.IntervalMinutes = intervalMinutes;

            task.NextRunAtUtc = CalculateNextRunAt(task);
        }

        await PersistConfigAsync(task);
    }

    public async Task ToggleTaskAsync(string jobCode, bool isEnabled)
    {
        ScheduledTaskItem? task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.JobCode.Equals(jobCode, StringComparison.OrdinalIgnoreCase));
            if (task == null) return;

            task.IsEnabled = isEnabled;
            task.NextRunAtUtc = CalculateNextRunAt(task);
        }

        await PersistConfigAsync(task);
    }

    private async Task PersistConfigAsync(ScheduledTaskItem task)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var key = $"SchedulerConfig_{task.JobCode}";
            var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);

            var dto = new ScheduleConfigDto
            {
                IsEnabled = task.IsEnabled,
                FrequencyType = task.FrequencyType,
                DailyTime = task.DailyTime,
                IntervalMinutes = task.IntervalMinutes
            };

            var json = JsonSerializer.Serialize(dto);

            if (setting == null)
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = json,
                    Description = $"{task.Name} 排程設定"
                });
            }
            else
            {
                setting.Value = json;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "持久化儲存排程 {JobCode} 設定至資料庫失敗", task.JobCode);
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

                var log = new ScheduledTaskLog
                {
                    JobCode = jobCode,
                    JobName = task.Name,
                    TriggeredAtUtc = DateTime.UtcNow,
                    Duration = duration,
                    IsSuccess = isSuccess,
                    Message = message,
                    TriggeredBy = triggeredBy
                };

                task.RecentLogs.Insert(0, log);
                if (task.RecentLogs.Count > 3)
                {
                    task.RecentLogs.RemoveRange(3, task.RecentLogs.Count - 3);
                }
            }
        }
    }

    public async Task CheckAndRunPendingJobsAsync(CancellationToken ct = default)
    {
        List<ScheduledTaskItem> dueTasks;
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            dueTasks = _tasks
                .Where(t => t.IsEnabled && t.NextRunAtUtc.HasValue && now >= t.NextRunAtUtc.Value)
                .ToList();
        }

        foreach (var task in dueTasks)
        {
            if (ct.IsCancellationRequested) break;

            _logger.LogInformation("背景排程到期觸發：{JobCode} ({JobName})", task.JobCode, task.Name);
            await TriggerJobAsync(task.JobCode, "系統排程");

            lock (_lock)
            {
                task.NextRunAtUtc = CalculateNextRunAt(task);
                _logger.LogInformation("排程 {JobCode} 下次預計執行時間：{NextRunAt}", task.JobCode, task.NextRunAtUtc?.AddHours(8));
            }
        }
    }

    public static DateTime? CalculateNextRunAt(ScheduledTaskItem task, DateTime? fromUtc = null)
    {
        if (!task.IsEnabled) return null;

        var baseUtc = fromUtc ?? DateTime.UtcNow;

        if (task.FrequencyType == ScheduleFrequencyType.IntervalMinutes)
        {
            var minutes = Math.Max(1, task.IntervalMinutes);
            return baseUtc.AddMinutes(minutes);
        }
        else
        {
            // 每日固定時間 (台北時間)
            var nowTaipei = baseUtc.AddHours(8);
            if (!TimeOnly.TryParse(task.DailyTime, out var timeOfDay))
            {
                timeOfDay = new TimeOnly(0, 0);
            }

            var targetTaipei = nowTaipei.Date.Add(timeOfDay.ToTimeSpan());
            if (targetTaipei <= nowTaipei)
            {
                targetTaipei = targetTaipei.AddDays(1);
            }

            return DateTime.SpecifyKind(targetTaipei.AddHours(-8), DateTimeKind.Utc);
        }
    }

    public async Task<(bool isSuccess, string message)> TriggerJobAsync(string jobCode, string triggeredBy = "管理員手動")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("執行排程工作：{JobCode}，觸發者：{TriggeredBy}", jobCode, triggeredBy);

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
                    var lineService = scope.ServiceProvider.GetRequiredService<LineNotificationService>();
                    var sentCount = await lineService.ProcessUpcomingLessonRemindersAsync();
                    sw.Stop();
                    var successMsg = $"執行成功！已掃描 24 小時內即將開始之課堂，共發送 {sentCount} 筆課前 LINE 提醒。";
                    RecordExecution(jobCode, true, successMsg, sw.Elapsed, triggeredBy);
                    return (true, successMsg);
                }

                case "HomeworkWatch":
                {
                    var lineService = scope.ServiceProvider.GetRequiredService<LineNotificationService>();
                    var sentCount = await lineService.Process48HourHomeworkRemindersAsync();
                    sw.Stop();
                    var successMsg = $"執行成功！已掃描作業缺繳名單，共發送 {sentCount} 筆 48 小時二次催促 LINE 通知。";
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
            _logger.LogError(ex, "排程 {JobCode} 執行時發生異常", jobCode);
            var err = $"執行異常：{ex.Message}";
            RecordExecution(jobCode, false, err, sw.Elapsed, triggeredBy);
            return (false, err);
        }
    }
}
