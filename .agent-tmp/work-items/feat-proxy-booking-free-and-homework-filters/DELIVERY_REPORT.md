# 交付報告 (DELIVERY_REPORT.md)

## 任務目標回顧
1. **老師代學生預約**：多個按鈕可選擇不扣堂數，若選擇則不扣任何堂數，僅此功能適用。
2. **作業追蹤與催促**：勾選作業不佳學生，可篩選學校、年別、班級，學生選單依條件動態連動顯示。
3. **邏輯說明**：解答「近期作業追蹤與預約解決狀態」的業務運作邏輯。
4. **交付方式**：開新分支修改，完成後直接發出 Pull Request。

## 變更項目摘要
1. **業務核心層 (`src/PlatoTutoring.Web/Services/BookingService.cs`)**：
   - `CreateBookingAsync` 新增 `bool waiveCreditDeduction = false` 參數。
   - 權限防護：若傳入 `waiveCreditDeduction = true`，嚴格檢查操作者身分必須為老師或管理員，否則拋出例外防禦。
   - 堂數扣抵：若為免扣堂數預約，`creditsNeeded` 設為 0，不扣除付費或贈送堂數，餘額為 0 亦可順利代約。
   - 取消防退：若取消該預約，因原始 `CreditsDeducted == 0`，不退還任何堂數。
2. **頁面後端 (`src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml.cs`)**：
   - 載入欄位管理中的學校 (`SchoolOptions`)、年別 (`GradeOptions`)、班級 (`ClassOptions`) 下拉選項。
   - `OnPostProxyBookAsync` 支援 `bool waiveCreditDeduction = false` 參數並轉交 `BookingService`。
3. **頁面前端 (`src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`)**：
   - 老師代約彈窗（`#modalProxyBook`）：
     - 增加獨立操作開關：「選擇不扣堂數（特殊獎勵／專案代約）」。
     - 底部按鈕區提供「確認代預約」與專用「不扣堂數代預約」綠色按鈕，雙重支援單鍵免扣堂送出或勾選後送出。
   - 作業不佳學生名單（`#tab-teacher-homework`）：
     - 新增學校、年別、班級 3 個條件篩選下拉選單。
     - 實作前端 JavaScript 三級連動：選擇學校時連動篩選班級選單，學生清單即時根據學校、年級、班級條件過濾並更新筆數統計。
4. **單元測試 (`tests/PlatoTutoring.Tests/BookingAndLedgerTests.cs`)**：
   - 新增 `ProxyBook_WithWaiveCreditDeduction_DoesNotDeductCredits_AndResolvesHomework` 測試案例，覆蓋權限阻擋、零堂數代約成功、無流水帳扣款、名額減少、未解決作業自動標註為解決、取消無補退等完整流程。
   - 修復 `BatchRecordAttendance` 測試時段時間，全數 10 個測試 100% 通過。

## 驗證報告
- 建置：通過 (0 Error)
- 單元測試：10/10 通過
- 程式碼品質與隔離：嚴格遵循沙盒隔離與工作單流程規範。
