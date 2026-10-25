# 交付報告 (DELIVERY_REPORT)

## 任務摘要
- **Work Item ID**: `issue-scheduler-customization-and-automation-v6`
- **Branch**: `feat/scheduler-customization-and-automation-v6`
- **目標**:
  1. 排程掛載至系統背景常駐服務自動執行。
  2. 歷程改為「每筆排程下方呈現近三筆紀錄」，移除底部全域表格，修復 `:F0` 格式錯誤。
  3. 排程支援開關切換（啟用/停用）。
  4. 排程時間支援自訂（含間隔分鐘數如每 1 分鐘，以及每日固定時間）。

## 變更項目說明
1. **背景常駐服務掛載 (`SystemSchedulerBackgroundService.cs` & `Program.cs`)**:
   - 建立並註冊 `SystemSchedulerBackgroundService` 為 ASP.NET Core HostedService (`AddHostedService`)。
   - 使用 `PeriodicTimer` 每 10 秒自動巡檢所有啟用中的排程，當到達下次執行時間時自動非同步觸發該工作並更新下次時間。
2. **排程管理服務強化 (`ScheduleManagerService.cs`)**:
   - 支援 `ScheduleFrequencyType`：`IntervalMinutes` (每 N 分鐘) 與 `DailyTime` (每日固定時間，以台北時區 UTC+8 為基準)。
   - 支援設定持久化儲存於 `BK_SystemSettings`（key: `SchedulerConfig_{jobCode}`），免 SQL Migration，重啟仍保留設定。
   - 支援 `ToggleTaskAsync`（切換開關）、`UpdateTaskConfigAsync`（自訂時間與模式）。
   - 歷史紀錄改為每個排程私有維護其最新 3 筆歷史紀錄（`RecentLogs`）。
   - 四大排程工作皆對接真實業務邏輯：
     - `BonusExpiry`: 學期贈送堂數歸零（`CreditLedgerService`）
     - `WaitlistCleanup`: 候補超時名額釋放與自動遞補（`BookingService.ProcessExpiredWaitlistHoldsAsync()`）
     - `LessonReminder`: 課前 24 小時 LINE 提醒推播（`LineNotificationService.ProcessUpcomingLessonRemindersAsync()`）
     - `HomeworkWatch`: 48 小時作業缺繳二次催促（`LineNotificationService.Process48HourHomeworkRemindersAsync()`）
3. **系統管理畫面優化 (`Index.cshtml` & `Index.cshtml.cs`)**:
   - 移除底部全域排程日誌大表格。
   - 在排程卡片/列表每一項目的下方直接展示其「近 3 筆執行紀錄」，並修復執行耗時毫秒數展示。
   - 每筆排程新增「開關切換按鈕（啟用/停用）」及「設定時間按鈕」。
   - 提供自訂排程時間彈跳視窗（Modal `#modalEditSchedule`），可選擇「每 N 分鐘執行」或「每日固定時間執行」，支援每 1 分鐘等極短間隔。
4. **自動化測試擴充 (`ComprehensiveEnhancementTests.cs`)**:
   - 新增 `ScheduleManager_SupportsToggling_IntervalCustomization_AndPerJobRecentLogs` 測試。
   - 23 項測試全數綠燈通過。

## 驗證結果
- 測試結果：23/23 通過（0 失敗，0 略過）。
- 建置結果：Build Succeeded。
