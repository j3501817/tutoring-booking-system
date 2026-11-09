# Assumptions — issue-mobile-recurring-table-header

1. **週期預約列表表頭不整齊原因**：
   - `#recurringSlotsTable` 位於 `.table-responsive` 容器內，但沒有指定 `min-width`，且 `<th>` 沒有設置 `white-space: nowrap`。
   - 在窄螢幕手機模式（~360px 寬）下，瀏覽器將 5 欄硬塞進有限寬度，導致「選取」字樣直排、「日期時間 (台北)」折成兩行、「類型」折成直排、「進度與年別」與「剩餘名額」紛紛斷字，造成表頭高低錯落極不整齊。

2. **解決方案**：
   - 為 `#recurringSlotsTable` 設定 `min-width: 520px`，並在表頭 `th` 加上 `text-nowrap` 與合理固定欄寬。
   - 表頭精簡文字為「選取」、「日期時間」、「類型」、「進度與班別」、「剩餘名額」，並加上置中或對齊樣式。
   - 手機模式下在表格上方提示可左右滑動查看，並確保無時段時（空資料列）表頭依然整齊等寬不變形。
