# Test Report: issue-parent-calendar-nav-seven-days

## 偵測到的技術棧
- **架構**：.NET 10.0 / ASP.NET Core Razor Pages / EF Core / xUnit
- **執行指令**：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj /p:EnableSourceControlManagerQueries=false`

## 驗證結果
- 總測試數：37 項
- 通過：37 項 (Passed)
- 失敗：0 項
- 略過：0 項

## 涵蓋重點
1. **往後七天導覽**：支援從今天持續往後 +7, +14, +21... 暫時不限。
2. **往前七天限制**：往前退回時，若已經到達或早於今天，強制停止在今天（`CanGoPrevWeek = false`，且後端輸入過去日期一律防呆校正至今天）。
3. **既有全系統業務流程**：36 項既有測試全數通過。
