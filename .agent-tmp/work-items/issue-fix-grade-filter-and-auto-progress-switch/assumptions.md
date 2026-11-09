# Assumptions — issue-fix-grade-filter-and-auto-progress-switch

1. **家長週期預約「年別篩選」顯示中文**：
   - `Parent/Index.cshtml` 中 `#recurringGradeFilter` 下拉選單原本誤寫為 `<option value="@g">@g</option>`，導致輸出類別名稱 `PlatoTutoring.Web.Models.FieldOption`。
   - 修正為 `<option value="@g.Value">@g.Value</option>`，顯示中文年別（如「國一」、「國二」、「國三」），並支援篩選時比對包含該年別之班別。

2. **課程進度依起訖日期自動切換為「目前開放進度」**：
   - 以台北時間今日日期（`todayTpe`）為準，凡 `CourseProgress` 之 `WeekStartDate <= todayTpe && todayTpe <= WeekEndDate`（課程起訖包含今天，例如今天 `11/9`，起訖包含 `11/9`），系統自動將其切換為 `IsCurrentProgress = true`（目前開放進度）；反之若今日不在起訖範圍內，則自動設為 `IsCurrentProgress = false`（非開放期間）。
   - 於 `BookingService.SyncCurrentProgressesByDateAsync` 集中實作此自動切換邏輯，並在載入家長預約頁、成員與每週進度頁、老師教務頁、管理員頁、首頁儀表板及新增進度時自動執行，無需人工手動點擊切換按鈕。
