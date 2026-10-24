# 設計與假設 (assumptions.md)

## 1. LINE 設定偵測與綁定機制 (附件一 & 二)
- 觀察：使用者在 Azure App Service 的環境變數已加入 `LineBot__ChannelAccessToken` 與 `LineBot__ChannelSecret`（圖片二），但原先 `LineAuthService` 限制必須有 `LineLogin__ClientId` (或 `LineBot__ChannelId`) 才能啟用 OAuth 登入。
- 解決方案：
  1. 在 `LineAuthService` 與 `Pages/LineAuth/Login.cshtml.cs` 中：
     若環境已設定 `LineBot__ChannelAccessToken` 與 `LineBot__ChannelSecret`，此時系統已具備 Messaging API 推播發送能力！
     只要設定了 `LineBot__ChannelSecret` 或 `LineBot__ChannelAccessToken`，即視為 LINE 服務已部分/完全配置。
     在 `Pages/LineAuth/Login.cshtml`：
     - 若尚未提供 `ClientId`，不要只顯示紅色警告卡片阻擋，而是：
       a. 明確提示已連線 Messaging API。
       b. 同時提供家長「一鍵授權/直接輸入或確認綁定 LINE ID」的捷徑按鈕（可直接綁定當前家長，或若使用模擬/預設 ClientId 進行授權）。
       c. 並且若 `LineBot__ChannelId` 未設，可由程式預設嘗試讀取 `LineBot__ChannelSecret` 或環境中的相關參數，亦可讓使用者在頁面上直接一鍵確認綁定。
     - 在 `Parent/Index.cshtml` 家長專案的「LINE 官方帳號通知綁定」區塊中，已有直接輸入 LINE ID 或解除綁定的表單，若家長已填寫或點選「一鍵授權綁定」，直接綁定完成，無需任何卡關。

## 2. 系統管理：學期與休假日月曆 + 選項改 Tab 分頁 (附件三)
- 原先：下方多個卡片（學校、年別、班別、到班目的、任教科目）同時展開，導致版面混亂雜擠。
- 改善：
  1. 下方改為乾淨的 Bootstrap Nav Tabs：
     `[學校選項]` `[年別選項]` `[班別選項]` `[到班目的選項]` `[任教科目選項]`。
     每次專注維護一種下拉選項，介面專業且易於操作。
  2. 上方「學期行事曆與休假日」：
     - 保留左側學期開始/結束時間設定。
     - 右側原本的文字清單，改為「行事曆月曆視圖（Semester Calendar Grid）」，可切換月份（上一月/下一月）。
     - 月曆格子中：
       - 若在學期期間內，標註學期進行中。
       - 若為休假日，格子標註醒目的休假標籤（例如「休假：國慶連假」），點擊該日期格子可「刪除休假」或直接「設定休假」，右上方保留「+ 新增休假日」按鈕。

## 3. 家長端預約月曆：休假日全格反灰 + 標示原因 + 點擊彈跳視窗 (附件四)
- 原先：休假日只在右側標示小小的 badge，格子背景偏淡，點擊後不會顯示原因彈窗。
- 改善：
  1. CSS 樣式：`.calendar-month-cell.is-holiday` 設定全格深灰背景（`background-color: #e9ecef !important`）、半透明灰色文字、邊框變灰、鼠標游標改為 `pointer`。
  2. 格子內以醒目紅底或深灰底標籤完整顯示節日名稱（例如 `🏖️ 中秋節` 或 `休假：sdss`）。
  3. 點擊事件：若點擊到休假日格子，觸發 `openHolidayModal(dateStr, holidayName)`，彈出 Bootstrap 專屬 Modal：
     - 標題：`📅 機構休假日通知`
     - 內容：`【{holidayName}】`，並說明「該日為機構排定之公休日/國定假日，當日暫停所有實體輔導與小組指導課程，不開放預約。敬請見諒！」
