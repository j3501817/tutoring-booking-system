# DELIVERY_REPORT: 家長線上預約月曆點擊展開修復與狀態顯示優化

## 1. 任務說明與背景
針對家長端預約頁面（`/Parent/Index.cshtml`）解決以下三點問題：
1. **點了日期沒有顯示列表**：
   - 經查原先月曆格子使用 Razor C# 動態字串插值組合 `onclick`，在 Razor 輸出時雙引號與單引號被 HTML encode（轉成 `&quot;` / `&#x27;`），導致瀏覽器解析為非預期屬性，點擊完全無法觸發 JavaScript。
   - 修復方案：
     - 改用乾淨的標準 `onclick="selectCalendarDate(this.dataset.date, this.dataset.dateZh)"`，不經由 C# 拼接引號。
     - 於 `document` 加入全域事件委派（`e.target.closest('.calendar-month-cell.has-slots')`），即使家長點擊到日期數字、小組/團體徽章或空白處，皆能 100% 精準捕獲點擊事件並展開列表。
     - 加入全域鍵盤無障礙支援（Enter / 空白鍵）與 `focus-visible` 樣式。
     - 將函式明確綁定至 `window.selectCalendarDate` 與 `window.clearSelectedDate`。
2. **已預約狀態改至單筆時段內顯示**：
   - 從月曆日期格子頂部移除綠色「已約」徽章，保持整個月曆簡潔。
   - 在單筆時段卡片中，保留並加強「✓ 您已預約此時段」綠色大徽章、綠色邊框卡片、佔位進度條與「已在預約名單」按鈕。
3. **剩餘空位改至單筆時段內顯示**：
   - 從月曆日期格子底部移除「餘 X 空位」或「已滿額」文字，月曆格子只顯示小組與團體堂數。
   - 在單筆時段卡片中，於標題列直接加上「剩餘 X 空位」/「名額已滿」醒目徽章，並在右側保留清楚的佔位進度條與已約/容量比率（例：已約 1 / 3）。

## 2. 異動檔案摘要
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`:
  - 調整月曆格子：移除「已約」與「剩餘空位」提示，保留「小組 X」與「團體 X」標籤，加入標準 `this.dataset` 傳參及無障礙屬性。
  - 增強單筆卡片：於時段卡片標題列加入剩餘名額徽章（`剩餘 @slot.RemainingSeats 空位` / `名額已滿`）與已預約徽章。
  - 列表容器加入 `#noMatchSlotNotice` 提示區塊。
  - 重構腳本：引入全域事件委派、鍵盤無障礙支援、網址 query string 狀態維護與 `window` 匯出。
- `src/PlatoTutoring.Web/wwwroot/css/site.css`:
  - 增加 `.calendar-month-cell.has-slots:focus-visible` 鍵盤聚焦樣式。
- `.agent-tmp/work-items/feat-parent-calendar-slot-list-fix/`:
  - 沙盒工作單文件：`assumptions.md`、`status.md`、`TEST_REPORT.md`、`DELIVERY_REPORT.md`。

## 3. 驗證結果
- `dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj --no-restore`: 17/17 通過。
- `dotnet build --no-restore`: 0 錯誤建置成功。
- 嚴格遵守 Scope Control，無修改任何無關功能。
