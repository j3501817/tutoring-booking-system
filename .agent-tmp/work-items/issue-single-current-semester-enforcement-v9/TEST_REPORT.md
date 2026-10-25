# 測試與驗證報告 (TEST_REPORT)

## 1. 偵測到的技術棧
- **架構平台**：.NET 10.0 (C# 13, ASP.NET Core Razor Pages, Entity Framework Core 9)
- **測試框架**：xUnit + EF Core In-Memory Database

## 2. 選擇驗證指令的理由
- 依據 `AGENTS.md` 規則 7.1 與 7.3，.NET 專案應使用 `dotnet test` 執行單元與回歸測試。

## 3. 實際執行的指令與結果
- 指令：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj --no-restore /p:EnableSourceControlManagerQueries=false`
- 結果：**通過 (Pass)**
  - 總計測試案例：24 項
  - 通過：24 項
  - 失敗：0 項
  - 略過：0 項
  - 執行耗時：762 ms

## 4. 驗證情境涵蓋
1. **新學期設定當前學期**：
   - 建立學期 1（設為目前學期）→ `sem1.IsCurrent == true`。
2. **新增第二學期切換當前**：
   - 建立學期 2（設為目前學期）→ `sem2.IsCurrent == true`，原學期 1 自動轉為 `false`，全庫 `IsCurrent == true` 數量為 1。
3. **切換學期功能**：
   - 呼叫 `OnPostSwitchSemesterAsync(sem1.Id)` → 學期 1 恢復為 `true`，學期 2 轉為 `false`。
4. **唯一當前學期取消防呆**：
   - 編輯學期 1 時送出 `setAsCurrent = false`，觸發防呆阻擋保護，強制維持為 `true`，全系統始終維持恰好一個當前學期。

## 5. 是否需要 PM Review
- **否**：功能防呆精準，全部測試通過。
