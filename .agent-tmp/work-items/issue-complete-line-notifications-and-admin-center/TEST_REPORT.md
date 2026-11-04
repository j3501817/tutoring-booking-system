# Test Report: issue-complete-line-notifications-and-admin-center

## 測試環境
- 技術棧：.NET 10 (C# / ASP.NET Core Razor Pages / EF Core)
- 測試專案：`tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj`
- 執行指令：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj /p:EnableSourceControlManagerQueries=false`

## 測試結果
- 總測試數：**35**
- 通過 (Passed)：**35**
- 失敗 (Failed)：**0**
- 略過 (Skipped)：**0**
- 結果狀態：**全數通過 (100% Passed)**

## 驗證項目涵蓋
1. **多元學習狀況標記與 LINE 推播**：
   - 驗證 `FlagLearningStatusAsync` 支援「進度落差」、「觀念不清」、「輔導未完成」、「補課確認」、「作業不佳」，正確記錄至 `BK_HomeworkWatchItems` 與 `BK_LineNotificationLogs`。
2. **提醒預約後仍未預約排程**：
   - 驗證 `ProcessUnbookedRemindersAsync` 掃描未完成輔導預約學生名單，定時推播 `UnbookedReminder`。
3. **開課前兩日（48小時）提醒排程**：
   - 驗證 `ProcessTwoDaysBeforeLessonRemindersAsync` 掃描開課前兩日課堂，發送 `TwoDaysReminder` 並註記 24h 免費取消改期截止提醒。
4. **團體輔導新進度開放廣播**：
   - 驗證 `NotifyNewGroupProgressAsync` 向全體綁定家長發送新進度推播並寫入日誌記錄。
5. **堂數異動完整生命週期推播 (`CreditsChanged`)**：
   - 驗證儲值入帳（`RecordTopUpAsync`）
   - 驗證活動/表揚贈券核發（`ApplyManualAdjustmentAsync` / `GrantSemesterBonusAsync`）
   - 驗證預約扣堂與候補自動遞補轉正扣堂（`CreateBookingAsync` / `PromoteNextWaitlistEntryAsync`）
   - 驗證取消預約退還（`CancelBookingAsync`）
   - 驗證退班結清退費（`ExecuteDropoutSettlementAsync`）
6. **系統管理「通知管理中心」**：
   - 驗證 `SystemAdmin.IndexModel` 正確載入 `RecentLineLogs`（限制 10 筆內）與推播統計，支援手動一鍵重送失敗訊息。
