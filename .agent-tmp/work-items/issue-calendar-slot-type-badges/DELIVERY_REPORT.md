# Delivery Report: issue-calendar-slot-type-badges

## 變更摘要
1. **家長月曆格子輔導類型標籤優化**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
   - 修改內容：將月曆日期格內的徽章文字由 `小組 @summary.SmallGroupCount`、`團體 @summary.GroupCount` 改為純文字 `小組指導`、`團體輔導`。
   - 數量呈現規則：同天無論有幾堂該類型課程，均只顯示 1 個標籤，移除所有數字尾綴。
   - 標籤文字與月曆圖例保持 100% 一致。
2. **單元測試補充**：
   - 檔案：`tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`
   - 測試名：`CalendarDaySummary_ShouldDisplaySingleBadgePerTypeRegardlessOfCount`。
3. **資料庫 SQL**：
   - **無須執行任何 SQL 語法**。
