# Delivery Report — 家長三等分頁籤精簡命名與行事曆顯示當堂課時段

## 變更摘要
1. **家長端中間三個導覽頁籤精簡命名**：
   - 第一個頁籤：`線上預約`（原為 `線上預約時段`）
   - 第二個頁籤：`預約與候補`（原為 `我的預約與候補`）
   - 第三個頁籤：`堂數異動`（原為 `堂數異動流水帳`）
   - 三個頁籤字數為 `4 字 / 5 字 / 4 字`，在手機與桌機的三等分卡片中更加對稱、醒目且易讀。
2. **行事曆日期格改顯示「當堂課時段」**：
   - 由於日曆上方已有顏色標示說明（藍色為小組指導、綠色為團體輔導），日期格內不再重複顯示「小組指導」或「團體輔導」文字，改為直接顯示當堂課的台北時段（如 `18:00-19:30`、`17:00-19:00`），並維持藍色（`.badge-slot-small`）/ 綠色（`.badge-slot-group`）徽章底色區分課堂類型。
3. **示範時段自動平移 SQL Server 相容性優化**：
   - 修正 `DbInitializer.RefreshExpiredDemoSlotsAsync` 在 SQL Server 下避免產生 `OPENJSON` 語法，確保線上環境當週無未來課堂時可自動將示範課堂平移至本週時段。

## 修改檔案摘要
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`：更新三個導覽 Tab 名稱與日曆日期格顯示當堂課時段（`HH:mm-HH:mm`）。
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`：於 `CalendarDaySummary` 新增 `Slots` 清單供日期格直接渲染各堂課時段。
- `src/PlatoTutoring.Web/wwwroot/css/site.css`：新增 `.calendar-slot-time-badge` 等寬數字（`tabular-nums`）與緊湊字距樣式。
- `src/PlatoTutoring.Web/Data/DbInitializer.cs`：優化 `RefreshExpiredDemoSlotsAsync` SQL Server 相容性。
- `tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`：新增 `CalendarDaySummary_Slots_DisplaysSlotTimeRangeInsteadOfTypeName` 單元測試。

## 測試與驗證結果
- `dotnet test`：**39 / 39 通過**

## 已知風險與是否需 PM 裁定
- 無高風險變更，不影響其他功能，無需額外人工裁定。
