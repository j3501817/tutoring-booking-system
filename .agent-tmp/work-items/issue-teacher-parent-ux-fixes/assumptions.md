# 需求與範圍定義 (assumptions.md)

### 範圍與變更點分析

1. **老師教務 - 建立輔導時段驗證（附件一）**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`
   - 目前在按「確認建立時段」時未做客戶端勾選驗證，直接 submit 到後端，後端因缺少進度而重定向帶錯誤訊息，導致 modal 被關閉且表單被重置。
   - 修正：在前端表單送出（或點擊確認建立時段按鈕）時，檢查是否有勾選至少一項課程進度，若未勾選則在 modal 內部顯示錯誤警示或用 alert 提醒，並阻止表單送出，保留使用者的輸入。

2. **老師教務 - 建立時段後停留在原本的 tab（附件二）**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml.cs` & `Index.cshtml`
   - 目前 `OnPostCreateSlotAsync` 重定向回 `/Teacher/Index`，沒有指定分頁，預設會打開第一個分頁「今日課堂點名」。
   - 修正：重定向帶上 `#tab-teacher-slots`，並在前端載入時恢復 `#tab-teacher-slots` 分頁。

3. **家長端 - 當日課堂列表顯示候補狀態與順位（附件三）**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml` & `Index.cshtml.cs`
   - 目前「當日課堂列表」只要額滿就顯示「登記候補」按鈕，即使該學生已經在該時段的候補名單中。
   - 修正：比對該學生是否已在候補清單（`StudentWaitlistEntries` 中 `TutoringSlotId == slot.Id` 且狀態為 `Waiting` 或 `NotifiedHold`），若已在候補，顯示「已在候補中（排隊順位：第 X 位）」徽章，避免重複登記或誤解。

4. **家長端 - 孩子顯示狀態標籤 & 無法預約/候補改以彈窗提示（附件四）**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
   - 孩子切換卡片上補上學籍狀態標籤（在學 / ⚠️未續報 / ❌已退班）。
   - 對於未續報或已退班的學生，點擊預約或登記候補時，直接在前端彈出「無法預約／候補」Modal 提示「該學生本學期尚未完成續報（或已退班），暫無法登記時段；請先於學生管理辦理續報」，而非僅在頁面頂端 reload 顯示橫幅。

5. **全域 Loading 遮罩取消時不觸發（附件五）**：
   - 檔案：`src/PlatoTutoring.Web/Pages/Shared/_Layout.cshtml` & `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
   - `_Layout.cshtml` 的全域 `document.addEventListener('submit')` 未檢查 `e.defaultPrevented`。當使用者在 `confirm(...)` 點選「取消」時，雖然表單送出被取消，但全域監聽器仍呼叫了 `showGlobalLoading`，導致全螢幕卡在等待中畫面。
   - 修正：在 `submit` 監聽器中先檢查 `if (e.defaultPrevented) return;`，並且在解除綁定表單加上 `data-no-loading="true"` 雙重防護。
