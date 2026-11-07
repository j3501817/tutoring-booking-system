# Test Report: issue-parent-calendar-seven-days

## 偵測到的技術棧
- **架構**：.NET 10.0 / ASP.NET Core Razor Pages / EF Core / xUnit
- **執行指令**：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj /p:EnableSourceControlManagerQueries=false`

## 驗證結果
- 總測試數：36 項
- 通過：36 項 (Passed)
- 失敗：0 項
- 略過：0 項

## 涵蓋重點
1. **家長行事曆 7 天範圍**：驗證 `SevenDays` 嚴格由今天起算共 7 天（Today ~ Today+6），不含過去日期。
2. **課堂類型徽章**：維持小組指導、團體輔導獨立徽章顯示。
3. **既有全系統業務流程**：35 項涵蓋預約、取消、候補、LINE 通知、堂數流水帳之既有測試全數通過。
