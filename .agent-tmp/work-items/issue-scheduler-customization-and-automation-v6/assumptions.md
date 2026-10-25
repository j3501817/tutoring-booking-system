# 假設與技術設計說明 (Assumptions & Technical Design)

work-item-id: issue-scheduler-customization-and-automation-v6
branch: feat/scheduler-customization-and-automation-v6

## 1. 排程掛載 (Background Service Mounting)
- 現有 `SemesterBonusExpirationBackgroundService` 可整合或由 `SystemSchedulerBackgroundService` 統一調度。
- `SystemSchedulerBackgroundService` 註冊為 `IHostedService`，內部採用每 10 秒 ~ 30 秒的檢查輪詢迴圈（或 `PeriodicTimer`），比對每個已啟用排程的 `NextRunAtUtc`。
- 一旦 `DateTime.UtcNow >= task.NextRunAtUtc` 且 `task.IsEnabled`，立即觸發 `TriggerJobAsync(task.JobCode, "系統排程")`，並根據其排程規則計算下一次執行時間。

## 2. 排程時間與開關自訂 (Schedule Customization & Toggling)
- 支援頻率類型：
  1. `IntervalMinutes`：每 N 分鐘執行一次（如每 1 分鐘、每 5 分鐘、每 30 分鐘...），下次執行時間 = 當前時間 + N 分鐘。
  2. `DailyTime`：每日固定台北時間執行（如 00:00, 08:00, 17:00 等），下次執行時間 = 該特定時間的下一個到達點。
- 支援開關：`IsEnabled` (bool)，開關切換可透過 AJAX 或 POST 表單即時更新。
- 儲存持久化：排程開關與時間參數儲存於已存在的 `BK_SystemSettings` 資料表（鍵值：`SchedulerConfig_{JobCode}`，JSON 儲存），無需執行任何 SQL Migration。

## 3. UI 歷程呈現 (Recent 3 Logs Per Job)
- 移除底部全域大表格，改在每筆排程項目內部或下方展示「最近 3 筆執行紀錄」。
- 每一筆紀錄包含：執行時間 (台北時間)、觸發來源 (系統排程 / 管理員手動)、耗時 (毫秒, 修正 `:F0` 格式)、狀態標籤 (成功 / 失敗)、結果摘要。
