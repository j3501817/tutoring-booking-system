# 交付報告 (DELIVERY REPORT)

## 任務摘要
針對使用者提報的 5 項教師端與家長端操作體驗與 UX 瑕疵進行針對性修復：

1. **老師教務與儲值（附件一）：建立輔導時段防呆提醒**
   - **問題**：在未勾選所屬課程進度時點選「確認建立時段」，系統會直接關閉彈跳視窗並於頁面頂部顯示提醒，造成表單所有已輸入項目（日期、時間、類型、備註）清空遺失。
   - **解法**：在 `#modalCreateSlot` 頂部新增專屬 `#createSlotErrorAlert` 警告框，並於表單加上 `onsubmit="return validateCreateSlotForm(event);"` 前端攔截。若未勾選進度，立即在彈跳視窗內上方標紅提示，阻止關閉視窗與表單提交，完整保留使用者已填寫的日期、時間與備註。勾選後或再次開啟視窗時自動清除錯誤。

2. **老師教務與儲值（附件二）：建立時段後留在原本的 tab**
   - **問題**：在「時段排課與代約」頁籤建立時段後，頁面跳轉回第一個頁籤「今日課堂點名」。
   - **解法**：在 `Teacher/Index.cshtml.cs` 的 `OnPostCreateSlotAsync`、`OnPostUpdateSlotAsync`、`OnPostDeleteSlotAsync`、`OnPostProxyBookAsync` 將 `RedirectToPage()` 改為 `RedirectToPage(null, null, null, "tab-teacher-slots")`，配合前端 URL hash 導航自動維持在「時段排課與代約」頁籤。

3. **家長預約與堂數（附件三）：若已在候補顯示候補中與順位**
   - **問題**：名額已滿的課堂，若學生已在候補名單中，仍顯示「登記候補」按鈕，重複點選報錯且無法得知目前候補順位。
   - **解法**：在當日課堂卡片中比對 `Model.StudentWaitlistEntries`，若該學生已在候補名單（Waiting 或 NotifiedHold），頂部狀態顯示「候補中（順位：#N）」，右側操作區顯示黃色候補資訊卡「排隊候補中（順位：#N）」，並隱藏「登記候補」按鈕。

4. **家長預約與堂數（附件四）：孩子卡片顯示狀態，無法報名改以彈跳視窗提示**
   - **問題**：關聯孩子卡片未呈現學籍狀態；未續報或已退班學生在預約/候補失敗時，錯誤顯示於最上方紅色橫幅。
   - **解法**：
     - 在孩子切換卡片與下拉選單中為學生標註狀態徽章（`在學`、`⚠️ 未續報`、`❌ 已退班`）。
     - 新增無法預約/候補專用 Modal（`#modalEnrollmentBlocked`）。當學生狀態為未續報或已退班時，點擊「確認預約」、「登記候補」或「週期預約」時，前端直接阻止送出並彈出該視窗說明；若後端回傳續報/退班限制訊息，亦寫入 `TempData["EnrollmentBlockedMessage"]` 由前端自動彈出視窗，絕不在頁面上方顯示紅色操作提醒橫幅。

5. **家長預約與堂數（附件五）：解除綁定選取消不出現等待中畫面**
   - **問題**：按解除 LINE 綁定後，在確認對話框選擇「取消」，畫面仍彈出全螢幕「處理中，請稍候...」等待遮罩。
   - **解法**：在 `_Layout.cshtml` 的全域表單 submit 事件監聽器開頭加入 `if (e.defaultPrevented) return;`，並於解除綁定表單標註 `data-no-loading="true"`。當使用者於 `confirm()` 點擊取消時，事件 defaultPrevented 為 true，立即終止執行，不呼叫 `showGlobalLoading()`，直接關閉對話框留在原畫面。

## 修改檔案清單
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml.cs`
- `src/PlatoTutoring.Web/Pages/Shared/_Layout.cshtml`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`
- `tests/PlatoTutoring.Tests/ComprehensiveEnhancementTests.cs`
