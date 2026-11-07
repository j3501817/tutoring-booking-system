# Delivery Report: issue-parent-calendar-nav-seven-days

## 需求概述
- 依據 PM 指示：「要可以往後七天跟往前七天。只是往前只到今天為止，往後暫時不限」。
- 其餘預約、時段展開、週期預約等既有功能皆不動。

## 變更項目
1. **`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`**
   - 擴充 `OnGetAsync` 接收 `string? startDate` 參數。
   - 增加 `CurrentStartDate`、`CurrentEndDate`、`PrevWeekStartDate`、`NextWeekStartDate`、`CanGoPrevWeek` 與 `IsCurrentWeek`。
   - 嚴格落實防呆：若 `startDate < TodayDate`，強制修正為 `TodayDate`（往前只到今天為止）。
   - 往前 7 天計算：`CurrentStartDate.AddDays(-7) < TodayDate ? TodayDate : CurrentStartDate.AddDays(-7)`，當處於今天這週時禁用往前按鈕。
   - 往後 7 天計算：`CurrentStartDate.AddDays(7)`，往後不設限。
   - 更新預設選定日期邏輯：自動鎖定當前 7 天範圍內最近有課的一天，讓時段列表直觀展示。
2. **`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`**
   - 在月曆卡片標頭加入**「往前七天」**、**「本週」**、**「往後七天」**按鈕組。
   - 「往前七天」在當前週時顯示為 disabled 反灰狀態，提示「往前只到今天為止」。
   - 切換週次時支援頁籤錨點 `asp-fragment="tab-parent-book"`，畫面平滑不跳動。
   - 標題動態顯示當前 7 天範圍（例如：`（11/7 ～ 11/13）`）。
3. **`tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`**
   - 新增 `SevenDaysNavigation_ForwardUnconstrained_And_BackwardStopsAtToday` 單元測試，驗證邊界與防呆。
