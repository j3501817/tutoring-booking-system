using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Services;

/// <summary>
/// 系統全域背景排程常駐服務：
/// 常駐於伺服器背景，定期巡檢排程設定，全自動排程執行到期任務（包含每 N 分鐘執行與每日固定時間執行）。
/// </summary>
public class SystemSchedulerBackgroundService : BackgroundService
{
    private readonly IScheduleManagerService _scheduleManager;
    private readonly ILogger<SystemSchedulerBackgroundService> _logger;

    public SystemSchedulerBackgroundService(
        IScheduleManagerService scheduleManager,
        ILogger<SystemSchedulerBackgroundService> logger)
    {
        _scheduleManager = scheduleManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("系統全域背景排程常駐服務已啟動。");

        // 伺服器啟動後先等待 10 秒，確保資料庫初始化完成
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            // 系統啟動時載入資料庫持久化之排程設定
            await _scheduleManager.LoadConfigurationsFromDbAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入排程持久化設定時發生異常");
        }

        // 常駐計時器：每 10 秒輪詢檢查是否有到期需要執行的排程
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await _scheduleManager.CheckAndRunPendingJobsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "背景排程巡檢循環發生異常");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("系統全域背景排程常駐服務已停止。");
    }
}
