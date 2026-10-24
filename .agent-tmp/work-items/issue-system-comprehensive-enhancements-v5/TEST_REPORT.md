# 測試驗證報告 (Test Report)

work-item-id: issue-system-comprehensive-enhancements-v5
branch: feat/system-comprehensive-enhancements-v5

## 1. 偵測到的技術棧
- **架構**：ASP.NET Core Razor Pages (.NET 10.0), Entity Framework Core
- **前端**：Bootstrap 5, Bootstrap Icons, Vanilla JS
- **測試框架**：xUnit, Microsoft.EntityFrameworkCore.InMemory

## 2. 驗證指令與執行結果
1. **編譯驗證**：
   - 指令：`DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore /p:EnableSourceControlManagerQueries=false`
   - 結果：✅ 建置成功，0 個錯誤。
2. **單元測試驗證**：
   - 指令：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj --no-restore /p:EnableSourceControlManagerQueries=false`
   - 結果：✅ 通過 22 個測試，0 失敗，0 略過。

## 3. 功能測試細節
1. **排程管理服務與介面驗證**：
   - 驗證 `ScheduleManagerService` 成功註冊並初始化 4 個排程項目（贈送堂數歸零、候補釋放遞補、課前提醒、作業催促）。
   - 驗證 `TriggerJobAsync("BonusExpiry")` 能夠手動觸發執行並正確記錄執行歷程（耗時、成功狀態、摘要訊息）。
2. **系統欄位管理顯示驗證**：
   - 閉合 `subtab-calendar` 的 card 與 tab-pane，確認 `subtab-fields` 為同層級獨立 Tab，在 DOM 切換時不再被包含在行事曆中而被隱藏。
3. **家長預約介面驗證**：
   - 確認移除右上角「目前開放進度」黑底標籤。
   - 確認移除線上預約時段按鈕上的數字 badge。
   - 確認 `StudentBookings` 僅包含 `StartsAtUtc >= todayStartUtc` 的今天及未來課堂，且依時間升冪排序。
4. **老師教務中心驗證**：
   - 驗證「尚有名額輔導時段」僅統計今天以後的時段。
   - 驗證時段列表預設過濾今天以後，排序為「可預約課堂優先排前，再依時間升冪排序」。
   - 驗證「作業追蹤與催促」數字僅顯示未解決數量；若已預約解決且課程已結束，不再顯示於清單中。
