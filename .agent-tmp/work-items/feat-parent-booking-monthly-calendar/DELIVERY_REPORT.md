# DELIVERY_REPORT: 家長線上預約月曆檢視與點選展開功能

## 1. 任務說明與背景
將家長預約與堂數頁面（`/Parent/Index.cshtml`）之「線上預約時段」頁籤改為：
1. **優先顯示月曆**：提供清楚之月份格線（含上月/下月導航）。
2. **標示當天課堂類型與餘額**：若某日有課堂，於該日期格子清楚標註「小組 X 堂」與「團體 X 堂」，並標示「已約」或剩餘空位。
3. **點日期之後展開當天課堂列表**：預設未點選時僅顯示月曆與引導卡片，點選具備課堂的日期後平滑展開該日課堂卡片與預約按鈕，並提供收合按鈕。

## 2. 異動檔案摘要
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`:
  - 增加 `CalendarDaySummary` 彙總結構與當月日期摘要字典 `CalendarDaysSummary`。
  - 在 `OnGetAsync` 計算當月天數、週首位移、各日小組/團體堂數與學生已約狀態。
  - 提供 `AllCurrentSlots` 供前端即時依點選日期切換。
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`:
  - 重構預約區塊，改為上月曆、下列表結構。
  - 新增月曆格子（含日期、小組/團體標籤、已約徽章、剩餘空位）。
  - 新增 `#noDateSelectedPrompt`（未選日期時引導提示）與 `#daySlotsContainer`（點選後展開當天課堂列表）。
  - 新增 `selectCalendarDate(dateStr, dateZh)` 與 `clearSelectedDate()` JavaScript 互動邏輯，支援即時卡片過濾與網址 query string 同步。
- `src/PlatoTutoring.Web/wwwroot/css/site.css`:
  - 新增月曆專用響應式樣式（`.calendar-month-grid`, `.calendar-month-cell`, `.has-slots`, `.selected-day`, `.badge-slot-small`, `.badge-slot-group` 等）。
- `tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`:
  - 新增針對月曆資料彙總計算、小組與團體堂數統計、滿額判定與個人已約狀態之單元測試。

## 3. 驗證結果
- `dotnet test`: 17/17 全部通過（包含新增之 2 個月曆測試）。
- `dotnet build`: 0 錯誤。
- 嚴格遵守 Scope Control，無修改任何無關功能。
