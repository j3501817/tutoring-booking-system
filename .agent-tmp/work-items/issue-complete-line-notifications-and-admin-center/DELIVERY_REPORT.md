# Delivery Report: issue-complete-line-notifications-and-admin-center

## 變更摘要
1. **補齊未完成與半套之 LINE 官方帳號通知**：
   - **堂數異動通知完整串接**：
     - 儲值入帳（`CreditLedgerService.RecordTopUpAsync`）
     - 獲得贈券 / 手動贈送 / 續報贈送（`CreditLedgerService.GrantSemesterBonusAsync`, `GrantSemesterBonusToActiveStudentsAsync`, `ApplyManualAdjustmentAsync`, `RenewStudentAsync`）
     - 完成預約扣堂 / 候補自動轉正扣堂（`BookingService.CreateBookingAsync`, `PromoteNextWaitlistEntryAsync`）
     - 取消預約退還（`BookingService.CancelBookingAsync`）
     - 退班結清退費（`CreditLedgerService.ExecuteDropoutSettlementAsync`）
   - **多元學習狀況標記與推播**：
     - 擴充 `BK_HomeworkWatchItems` 欄位 `ConditionType`（支援：作業不佳、進度落差、觀念不清、輔導未完成、補課確認）。
     - 老師後台介面提供狀況下拉選單與備註填寫，即時發送 `LearningStatusAlert` / `HomeworkPoor` 推播。
   - **提醒預約後仍未預約排程**：
     - 新增 `ProcessUnbookedRemindersAsync` 掃描未完成輔導預約名單，定時推播 `UnbookedReminder`，並於系統排程與老師後台提供手動觸發。
   - **開課前兩日（48小時）提醒排程**：
     - 實作 `ProcessTwoDaysBeforeLessonRemindersAsync` 掃描約兩日後之課堂，推播到班提醒並載明「尚有 1 日寬限期可免費線上取消改期」。
   - **團體輔導新進度開放預約廣播**：
     - `NotifyNewGroupProgressAsync` 擴充日誌寫入，並於管理員建立新進度時提供一鍵廣播通知全體家長。
2. **系統管理新增「通知管理中心」**：
   - 於「系統管理」(`/SystemAdmin`) 新增獨立分頁「通知管理」（`#subtab-notifications`）。
   - 上半部：詳細列出系統支援之 9 大 LINE 官方帳號通知情境、觸發時機、接收對象與樣板預覽。
   - 下半部：展開顯示最新 10 筆 LINE 通知紀錄（Accordion 點擊展開可檢視全文、收件者 LINE ID、冪等防重鍵與重試次數，失敗提供一鍵手動重送）。
   - 左側側邊欄子選單與管理員報表頁同步加入導覽連結。
3. **資料庫 SQL 升級檔案**：
   - 產出專屬遷移腳本：`docs/database/migration_20261104_notifications_and_condition_types.sql`，可直接於生產環境 Azure SQL 執行。
