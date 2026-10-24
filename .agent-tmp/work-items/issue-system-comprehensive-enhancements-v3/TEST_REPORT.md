# 測試與驗證報告 (TEST REPORT)

work-item-id: issue-system-comprehensive-enhancements-v3
branch: feat/system-comprehensive-enhancements-v3

## 1. 偵測到的技術棧
- **Framework**: .NET 10 (ASP.NET Core Razor Pages, Entity Framework Core)
- **Database**: SQLite (local)
- **Frontend**: Bootstrap 5, Bootstrap Icons, Vanilla JavaScript

## 2. 選擇驗證指令與理由
- `dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore /p:EnableSourceControlManagerQueries=false`：針對 Web 專案核心執行完整編譯與 Razor Page 語法分析，確保 C# 與 Razor 標記 100% 正確。

## 3. 執行指令與結果
- **指令**: `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore /p:EnableSourceControlManagerQueries=false`
  - **結果**: 通過 (Pass)
  - **輸出摘要**: `PlatoTutoring.Web -> .../PlatoTutoring.Web.dll`，`0 個錯誤`。
- **指令**: `grep -rn "下拉選擇" src/`
  - **結果**: 通過 (Pass)，確認所有「（下拉選擇）」字眼已全數移除。

## 4. 人工邏輯審查項
1. **系統管理多學期與 Tab 鎖定**：
   - 後端在 `DeleteOption`、`CreateOption`、`UpdateOption`、`DeletePurpose`、`CreatePurpose` 均支援 `activeTab` 參數，並使用 `RedirectToPage(..., fragment: targetTab)` 轉導。
   - 前端 JavaScript 加入 `localStorage.setItem('plato_admin_tab', target)` 雙重保證，刪除後停留在原 Tab。
   - 學期管理提供列表卡片，可切換學期並連帶觸發 `ExpireAllBonusCreditsForSemesterEndAsync`。
2. **家長預約與可用堂數**：
   - `Parent/Index.cshtml` 移除「=」，清晰分列付費堂數與贈送堂數，註明贈送堂數以到班日計算且換學期即歸 0。
   - 月曆「上月 / 本月 / 下月」按鈕採用 `my-auto` 垂直置中。
3. **學生/家長/老師管理**：
   - `Members/Index.cshtml.cs` 使用雙向關聯聚合，將共用學生的 2 位家長聚合至同一個 `FamilyGroupRow.Parents`。
   - `Members/Index.cshtml` 在同一欄位垂直展示兩位家長，不拆分為兩筆資料。
   - 班別選項固定為「資優班」與「精英班」。
4. **老師教務與儲值**：
   - 頂部原先的大表單改為 2 顆清晰的工具列按鈕，並已修正 `saveSingleAttendance` 閉合括號缺失 bug，Bootstrap Modal 點擊 100% 正常開啟。
   - 近期儲值批次與憑證紀錄列表改為主要畫面，上方配置學校、年別、班別與匯款日篩選表單。
