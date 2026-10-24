using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

/// <summary>
/// 每日台北時間 00:00 定時排程服務：
/// 自動檢查學期結束日期，若學期已屆滿結束，將所有未用完之贈送堂數自動歸零並記錄流水帳。
/// </summary>
public class SemesterBonusExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SemesterBonusExpirationBackgroundService> _logger;
    private readonly IScheduleManagerService _scheduleManager;

    public SemesterBonusExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SemesterBonusExpirationBackgroundService> logger,
        IScheduleManagerService scheduleManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _scheduleManager = scheduleManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("學期贈送堂數定時排程服務已啟動。");

        // 伺服器啟動後先等待 15 秒，避免與 DbInitializer 衝突，隨即執行一次開機過期預檢
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            await CheckAndExpireBonusCreditsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開機執行學期過期預檢時發生異常");
        }

        // 每日循環：計算到下一個台北時間 00:00:05
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                var nowTaipei = nowUtc.AddHours(8);
                var nextMidnightTaipei = nowTaipei.Date.AddDays(1).AddSeconds(5);
                var delay = nextMidnightTaipei - nowTaipei;

                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromHours(24);
                }

                _logger.LogInformation("下次學期贈送堂數自動過期檢查時間 (台北時間)：{NextRunTime} (約 {Hours:F1} 小時後)", nextMidnightTaipei, delay.TotalHours);

                await Task.Delay(delay, stoppingToken);

                await CheckAndExpireBonusCreditsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "每日 00:00 執行學期贈送堂數過期檢查時發生異常");
                // 遇到錯誤延遲 10 分鐘後重試，避免緊密迴圈
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }

    public async Task<int> CheckAndExpireBonusCreditsAsync(CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ledgerService = scope.ServiceProvider.GetRequiredService<CreditLedgerService>();

        var todayTaipei = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
        var expiredCount = 0;

        // 尋找已過期（結束日期小於今天）但未標記結束或目前仍標記為 Current 的學期
        var overdueSemesters = await db.Semesters
            .Where(s => s.EndDate < todayTaipei && (!s.IsEnded || s.IsCurrent))
            .OrderBy(s => s.EndDate)
            .ToListAsync(ct);

        if (!overdueSemesters.Any())
        {
            // 若沒有明確過期的學期記錄，但當前學期若結束日已過
            var currentSemester = await db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent, ct);
            if (currentSemester != null && currentSemester.EndDate < todayTaipei)
            {
                overdueSemesters.Add(currentSemester);
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (!overdueSemesters.Any())
            {
                _logger.LogInformation("目前無已過期學期需要清理贈送堂數。");
                sw.Stop();
                _scheduleManager.RecordExecution("BonusExpiry", true, "自動排程巡檢完成，目前所有學期均在效期內，無過期贈送堂數。", sw.Elapsed, "系統排程");
                return 0;
            }

            // 取得系統管理員作為操作人紀錄
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin, ct)
                        ?? await db.Users.FirstOrDefaultAsync(ct);

            if (admin == null)
            {
                _logger.LogWarning("資料庫中無任何使用者，無法標記贈送堂數歸零操作人。");
                sw.Stop();
                _scheduleManager.RecordExecution("BonusExpiry", false, "執行失敗：資料庫中無任何使用者，無法標記贈送堂數歸零操作人。", sw.Elapsed, "系統排程");
                return 0;
            }

            foreach (var sem in overdueSemesters)
            {
                _logger.LogInformation("學期「{SemesterName}」結束日 ({EndDate}) 已屆滿，自動執行贈送堂數歸零...", sem.Name, sem.EndDate);
                var count = await ledgerService.ExpireAllBonusCreditsForSemesterEndAsync(sem.Id, admin.Id);
                expiredCount += count;
                _logger.LogInformation("學期「{SemesterName}」贈送堂數已自動歸零完畢，影響學生數：{Count}", sem.Name, count);
            }

            sw.Stop();
            _scheduleManager.RecordExecution("BonusExpiry", true, $"學期結束自動歸零完畢，共巡檢 {overdueSemesters.Count} 個學期，影響 {expiredCount} 位學生。", sw.Elapsed, "系統排程");
            return expiredCount;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "執行學期過期清理時發生錯誤");
            _scheduleManager.RecordExecution("BonusExpiry", false, $"排程執行異常：{ex.Message}", sw.Elapsed, "系統排程");
            throw;
        }
    }
}
