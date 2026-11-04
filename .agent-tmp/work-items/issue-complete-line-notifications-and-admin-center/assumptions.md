# Assumptions & Technical Decisions: issue-complete-line-notifications-and-admin-center

## 1. 堂數異動通知（Point 1）
- 目前已存在 `LineNotificationService.NotifyCreditsChangedAsync`，但先前僅在 `BookingService.CancelBookingAsync`（取消預約退還）被呼叫。
- 補齊以下觸發點：
  1. **獲得贈券 / 儲值入帳**：在 `CreditLedgerService.RecordTopUpAsync`（儲值）以及 `CreditLedgerService.GrantBonusCreditsAsync`（贈送堂數）成功後，呼叫 `NotifyCreditsChangedAsync`。
  2. **完成預約扣堂**：在 `BookingService.BookSlotAsync`（包含一般預約扣堂）與 `PromoteNextWaitlistEntryAsync`（候補轉正扣堂）時，若有扣除付費或贈送堂數，除了預約成功通知外，亦發送堂數異動通知（或整合至堂數異動流水紀錄）。
  3. **退還費用 / 結算退班**：在 `CreditLedgerService.WithdrawStudentAsync`（學生退班結算/退款）中呼叫 `NotifyCreditsChangedAsync`。

## 2. 學生多元學習狀況通知（Point 3）
- 目前 `LineNotificationService.NotifyStudentLearningStatusAsync` 支援 `conditionType`（作業不佳、進度落差、觀念不清、輔導未完成、補課確認）。
- 老師後台（`Teacher/Index.cshtml` 與 `Teacher/Index.cshtml.cs`）原本僅有單純「作業不佳」按鈕。我們將其擴充為下拉選擇學習狀況（作業不佳、進度落差、觀念不清、輔導未完成、補課確認），並保留備註欄位，發送 `NotifyStudentLearningStatusAsync`，且同步在資料庫記錄追蹤項目（或擴充 `HomeworkWatchItem.SubjectNote` 與記錄），使家長能即時掌握狀況並點擊預約。

## 3. 提醒預約後仍未預約（Point 4）
- `LineNotificationService.NotifyUnbookedReminderAsync` 目前未被掛載。
- 實作在 `ScheduleManagerService` 中新增排程 `UnbookedReminder`，並在系統排程清單與管理員後台加入手動執行按鈕。
- 邏輯：掃描有被標記學習狀況（如作業不佳或其他學習狀況）且尚未有預約解決記錄者，發送未預約提醒。

## 4. 提醒兩日後有預約，記得到班或尚有一日可以取消改期（Point 5）
- `LineNotificationService.NotifyTwoDaysBeforeReminderAsync` 目前未被掛載。
- 在 `LineNotificationService.cs` 實作 `ProcessTwoDaysBeforeLessonRemindersAsync`：掃描開課時間在 48 小時至 72 小時後（約 2 天後）的預約，發送課前兩日提醒，告知尚有 1 日寬限期可取消改期。
- 掛載入 `ScheduleManagerService` 的每日自動排程或排程任務清單中，供定時掃描及後台一鍵觸發。

## 5. 團體輔導新進度開放預約（Point 6）
- `LineNotificationService.NotifyNewGroupProgressAsync` 廣播通知給所有綁定 LINE 的家長。
- 在管理員後台新增課程進度時 (`Admin/Index.cshtml.cs:OnPostCreateProgressAsync`)，以及新增團體輔導時段時，自動觸發或提供開關觸發此推播。

## 6. 系統管理「通知管理」分頁（Admin Notification Center）
- 在 `Admin/Index.cshtml` 導覽列加入「通知管理」（`#tab-admin-notifications`）。
- 內容包含：
  1. **LINE 通知支援清單一覽表**：列出全部支援的 LINE 通知情境（堂數異動、預約成功、候補自動轉正、學習狀況提醒、48h作業催促、未預約催促、課前兩日提醒、課前24h提醒、新進度公告）。
  2. **近 10 筆通知紀錄展開列表**：顯示最近 10 筆 LINE 通知紀錄（卡片或表格），可點擊展開查看詳細內容、完整訊息文字、發送狀態與重試按鈕。

## 7. 資料庫 SQL
- 檢查是否需要新增資料表欄位或預設資料。若無變更，撰寫說明；若有變更或需要檢視腳本，產出對應的 SQL 檔案。
