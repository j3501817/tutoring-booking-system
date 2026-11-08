# Delivery Report — 支援同時開放多個課程進度與系統自動建立並隱藏進度代碼

## 變更摘要
1. **進度切換支援同時多個開放進度（不會只有一個）**：
   - 原本切換目前開放進度為單選互斥（設某筆為目前進度時會將其他所有進度關閉）。
   - 現改為**每個進度可獨立切換「設為目前進度（開放）／關閉進度」**（Toggle `IsCurrentProgress`），開啟某個進度時**不會關閉其他已開放的進度**，且已開放的進度旁新增「關閉進度」按鈕，可隨時單獨停用。
   - 新增每週課程進度勾選「立即設為目前開放進度」時，同樣直接將新進度加入開放清單，不會關閉既有已開放進度。
   - 系統總覽儀表板（`Pages/Index.cshtml.cs`）支援同時顯示多個目前開放中的進度名稱。
2. **進度代碼由系統自行建立且不需要顯示**：
   - 移除「新增每週課程進度」表單中的「進度代碼（如 W09）」輸入欄位，改由後端自動依序產生唯一代碼（如 `W09`、`W10`...）。
   - 移除全站畫面（`Members` 學期與每週進度清單、`Parent` 家長預約與作業提醒、`Teacher` 老師課堂與進度選單、首頁儀表板、提示訊息）中顯示的進度代碼 `Code`，僅呈現進度主題名稱 `Title`。

## 修改檔案摘要
- `src/PlatoTutoring.Web/Pages/Members/Index.cshtml`：移除進度代碼輸入欄與清單中的代碼顯示，並為已開放進度提供「關閉進度」獨立切換按鈕。
- `src/PlatoTutoring.Web/Pages/Members/Index.cshtml.cs`：`OnPostSwitchCurrentProgressAsync` 改為獨立切換單筆進度開關（不關閉其他進度）；`OnPostCreateProgressAsync` 支援系統自動編號且不關閉既有開放進度。
- `src/PlatoTutoring.Web/Pages/Admin/Index.cshtml.cs`：同步更新進度切換與新增邏輯。
- `src/PlatoTutoring.Web/Pages/Index.cshtml.cs`：支援串接顯示多個目前開放進度名稱並隱藏代碼。
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`：隱藏課堂卡片、預約紀錄、週期預約與作業提醒中的進度代碼。
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml` & `Index.cshtml.cs`：隱藏老師端點名、時段清單、下拉選單與名單彈窗中的進度代碼。
- `tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`：新增 `CourseProgress_SupportsMultipleCurrentProgresses_And_AutoGeneratesCode` 單元測試。

## 測試與驗證結果
- `dotnet test`：**40 / 40 通過**

## 已知風險與是否需 PM 裁定
- 無高風險變更，不影響其他功能，無需額外人工裁定。
