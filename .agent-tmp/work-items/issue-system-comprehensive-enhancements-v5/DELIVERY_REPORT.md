# 交付報告 (Delivery Report)

work-item-id: issue-system-comprehensive-enhancements-v5
branch: feat/system-comprehensive-enhancements-v5

## 變更摘要

### 1. 系統管理 (System Admin)
- **新增「排程管理」獨立子功能**：
  - 於側邊欄導覽選單與系統管理頂部導覽列新增第 3 個子頁籤「排程管理」(`subtab-schedules`)。
  - 建立 `ScheduleManagerService`，納管系統內所有背景排程任務：
    1. 學期贈送堂數自動過期歸零排程 (每日 00:00 台北時間)
    2. 候補資格逾期釋放與自動遞補排程 (每 5 分鐘)
    3. 課前 LINE 推播提醒定時排程 (每日 08:00 台北時間)
    4. 作業缺繳 48 小時二次催促通知排程 (每日 17:00 台北時間)
  - 呈現完整排程名稱、代碼、執行週期、運行狀態、上次執行時間、執行耗時、詳細執行結果與下次預計執行時間。
  - 提供即時「手動立即執行」按鈕，並於下方展示「近期排程執行歷程紀錄」清單。
- **修復「系統欄位管理」為空的問題**：
  - 修正 `Index.cshtml` 中 `subtab-calendar` 遺漏的 4 個閉合 `</div>` 標籤，使 `subtab-fields` 成為同層獨立 Tab，解決被行事曆 Tab 嵌套而跟著隱藏全空的問題。

### 2. 家長預約與堂數 (Parent Workspace)
- **移除右上角開放進度標籤**：移除「目前開放進度：...」黑底標籤。
- **預約清單過濾**：預約清單過濾為「今天（含今天）以後」的課堂，歷史預約不再顯示於進行中清單，排序依時間升冪排列（即將到來的課堂排在前）。
- **時段按鈕數字移除**：移除「線上預約時段」頁籤按鈕上的數字 badge。

### 3. 老師教務與儲值 (Teacher Workspace)
- **頂部指標過濾**：「尚有名額輔導時段」指標僅統計今天（含今天）以後的時段。
- **時段排課列表優化**：
  - 列表預設顯示今天（含今天）以後的時段。
  - 排序方式改為：**優先顯示可預約（尚有名額）的課堂**，再依時間升冪排序。
- **作業追蹤與催促**：
  - 頁籤 badge 與頂部卡片數字統一改為顯示「非已預約解決」的待追蹤數量。
  - 若已預約解決且課程時段已結束，自動從作業追蹤列表中排除，維持清單乾淨精簡。

## 修改檔案清單
- `src/PlatoTutoring.Web/Services/ScheduleManagerService.cs` (新增)
- `src/PlatoTutoring.Web/Program.cs`
- `src/PlatoTutoring.Web/Services/SemesterBonusExpirationBackgroundService.cs`
- `src/PlatoTutoring.Web/Pages/Shared/_Layout.cshtml`
- `src/PlatoTutoring.Web/Pages/SystemAdmin/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/SystemAdmin/Index.cshtml.cs`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`
- `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml.cs`
- `tests/PlatoTutoring.Tests/ComprehensiveEnhancementTests.cs`

## 驗證結果
- `dotnet build`: 0 錯誤。
- `dotnet test`: 22 測試全部通過 (0 失敗)。
