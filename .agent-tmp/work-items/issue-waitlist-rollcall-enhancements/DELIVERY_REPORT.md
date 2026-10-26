# 交付報告：家長端候補排隊整合、點名選項精簡與老師端時段名冊彈窗 (DELIVERY_REPORT.md)

## 1. 需求變更摘要
1. **家長預約與堂數**：
   - **Tab 計數整合**：「我的預約與候補」頁籤徽章同時計入有效預約（Confirmed）與有效候補（Waiting / NotifiedHold）數量。
   - **候補順位與 LINE 通知分開**：右側專注作為「LINE 官方帳號通知」卡片，獨立呈現綁定狀態、User ID、解綁操作與即時推播歷程。
   - **預約與候補時間排序清單**：左側重構為整合清單，依時段開課時間（`StartsAtUtc`）統一排序。候補項目免預先扣堂，並清楚標註「排隊候補中（順位）」或「名額保留中（30分鐘截止倒數）」。
   - **取消候補與自動遞補**：提供取消候補操作，呼叫 `CancelWaitlistAsync`，取消後自動記錄於 `LineNotificationLog` 歷程，並將該時段後續候補同學順位自動向前遞補 1 位；若為保留名額則釋出並通知下一位。
2. **老師教務與儲值**：
   - **點名選項調整**：今日點名下拉選項移除「請假」，將「曠課爽約」精簡改為「曠課」。
   - **時段預約名額超連結與候補數**：時段列表中移除「（餘 X 名）」，新增「候補 X 人」徽章，並轉為可點擊之超連結。
   - **課堂名冊與候補名單彈窗**：點擊名額超連結彈出 `#modalSlotRoster`，即時呈現目前已報名名單（點名與扣堂狀態）及候補中名單（順位、到班目的）；若有保留中狀態（NotifiedHold），老師可直接代為確認轉正。

## 2. 修改檔案清單
- `src/PlatoTutoring.Web/Models/Entities.cs`
- `src/PlatoTutoring.Web/Data/DbInitializer.cs`
- `src/PlatoTutoring.Web/Services/BookingService.cs`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml.cs`
- `tests/PlatoTutoring.Tests/ComprehensiveEnhancementTests.cs`

## 3. 驗證結果
- `dotnet build`：0 錯誤，順利編譯。
- `dotnet test`：31 項單元測試全數 Passed。
