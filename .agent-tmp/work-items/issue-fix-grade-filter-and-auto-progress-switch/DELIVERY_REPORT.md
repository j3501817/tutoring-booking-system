# Delivery Report — 修正週期預約年別篩選中文顯示與課程進度依起訖日期自動切換開放狀態

## 變更摘要
1. **家長端週期預約「年別篩選」修正為中文顯示**：
   - `Parent/Index.cshtml` 的 `#recurringGradeFilter` 下拉選單原本誤寫為 `<option value="@g">@g</option>`，導致輸出物件型別名稱 `PlatoTutoring.Web.Models.FieldOption`。
   - 修正為 `<option value="@g.Value">@g.Value</option>`，正確顯示中文年別（如「國一」、「國二」、「國三」），並支援篩選時比對包含該年別之班別。
2. **課程進度依今日日期自動切換為「目前開放進度」（反之則否）**：
   - 新增 `BookingService.SyncCurrentProgressesByDateAsync()`：以台北時間今日日期（`todayTpe`）為準，凡課程起訖日期（`WeekStartDate ~ WeekEndDate`）包含今日者（如今天 `11/9`，起訖包含 `11/9`），自動切換為 `IsCurrentProgress = true`（目前開放進度）；反之若不包含今日，則自動切換為 `IsCurrentProgress = false`（非開放期間）。
   - 於家長預約頁、成員與每週進度頁、老師教務頁、管理員頁、首頁儀表板載入及新增進度時自動執行同步，無需人工手動點擊切換按鈕。

## 修改檔案摘要
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`：修正 `#recurringGradeFilter` 選項為 `@g.Value` 中文顯示，並強化前端年別比對邏輯。
- `src/PlatoTutoring.Web/Services/BookingService.cs`：新增 `SyncCurrentProgressesByDateAsync` 自動同步進度開放狀態。
- `src/PlatoTutoring.Web/Pages/Members/Index.cshtml` & `Index.cshtml.cs`：改為自動依日期切換開放進度並顯示「目前開放進度／非開放期間」狀態徽章。
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`、`Admin/Index.cshtml.cs`、`Teacher/Index.cshtml.cs`、`Pages/Index.cshtml.cs`：載入時自動執行 `SyncCurrentProgressesByDateAsync`。
- `tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`：新增 `CourseProgress_AutoSwitchesByTodayDateRange_And_GradeOptionUsesChineseValue` 單元測試。

## 測試與驗證結果
- `dotnet test`：**41 / 41 通過**

## 已知風險與是否需 PM 裁定
- 無高風險變更，不影響其他功能，無需額外人工裁定。
