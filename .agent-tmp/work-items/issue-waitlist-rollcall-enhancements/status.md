# 工作單狀態：issue-waitlist-rollcall-enhancements

## 任務目標
1. 家長預約與堂數：
   - 候補數字顯示出來（Tab 徽章計入有效候補人數）
   - 候補順位與 LINE 通知分開，右側獨立為 LINE 通知卡片
   - 候補整合進預約清單中，統一照日期時間排序
   - 支援取消候補，取消後記錄歷程，並自動遞補調整後續候補學生順位
2. 老師教務與儲值：
   - 點名下拉選項移除「請假」，曠課爽約改為「曠課」
   - 時段列表「預約名額」中「餘幾名」不需要，改加上「候補中幾名」，變成超連結
   - 點擊超連結彈跳視窗顯示「報名名單」跟「候補中名單」，若候補上（NotifiedHold）同步顯示狀態並提供老師代為確認轉正

## 進度 Checklist
- ✅ 建立專屬工作單分支 `feat/waitlist-rollcall-enhancements`
- ✅ 資料實體擴充：`WaitlistEntry` 新增 `CancelledAtUtc` 與 `CancelReason`，DB Initializer 相容檢查
- ✅ 後端邏輯：`BookingService.CancelWaitlistAsync` 取消候補、釋放保留名額、後續順位自動向前推進、記錄歷程到 `LineNotificationLog`
- ✅ 家長端前端與處理器：
  - `Parent/Index.cshtml.cs`：`OnPostCancelWaitlistAsync`
  - `Parent/Index.cshtml`：Tab 徽章計數加入候補；左側整合成預約與候補時間排序清單；右側獨立為 LINE 官方帳號通知卡片
- ✅ 老師端前端與處理器：
  - `Teacher/Index.cshtml.cs`：查詢時段候補人數、`OnGetSlotRosterDetailsAsync`、`OnPostTeacherConfirmWaitlistAsync`
  - `Teacher/Index.cshtml`：今日點名移除請假、爽約改曠課；名額欄位改為候補人數超連結；加入 `#modalSlotRoster` 彈窗與操作 JS
- ✅ 單元測試：新增 `CancelWaitlistAsync_CancelsEntry_ReordersSubsequentEntries_AndLogsHistory`，31 項測試 100% 通過
- ✅ 交付歸檔：產出 `TEST_REPORT.md` 與 `DELIVERY_REPORT.md`，提交 commit、push 並建立 PR
