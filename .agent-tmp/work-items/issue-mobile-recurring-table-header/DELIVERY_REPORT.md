# Delivery Report — 修正週期預約時段列表表頭手機模式折行不整齊問題

## 變更摘要
1. **週期預約時段清單表頭排版整齊化（手機模式防擠壓）**：
   - 表頭所有欄位（選取、日期時間、類型、進度與年別、剩餘名額）加上 `text-nowrap`，徹底杜絕窄螢幕下「選取」字樣直排、「日期時間 (台北)」折成多行、「類型」與「進度與年別」斷字錯位等不整齊問題。
   - 為表格容器指定合適欄寬，並在 `site.css` 為 `#recurringSlotsTable` 設定 `min-width: 520px`，確保在手機小螢幕上於 `.table-responsive` 容器內完整維持水平欄位結構，支援平順橫向滑動查看。
   - 在表格上方新增手機版滑動提示「可左右滑動查看」，兼顧桌機與手機雙端瀏覽體驗。
   - 即使在空資料（`!Model.AllCurrentSlots.Any()`）時，表頭 5 個欄位依然整齊對稱不變形。

## 修改檔案摘要
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`：表頭欄位文字優化（去除冗贅 `(台北)`），設置 `text-nowrap`、固定欄寬與手機滑動提示。
- `src/PlatoTutoring.Web/wwwroot/css/site.css`：新增 `#recurringSlotsTable` 樣式（`min-width: 520px`、表頭 padding 與底色邊框美化）。
- `tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`：新增 `RecurringSlotsTable_Headers_AreConfiguredWithNonBreakingLayout` 單元測試。

## 測試與驗證結果
- `dotnet test`：**42 / 42 通過**

## 已知風險與是否需 PM 裁定
- 無高風險變更，不影響其他功能，無需額外人工裁定。
