# Test Report: issue-mobile-parent-tabs-and-calendar

## 偵測到的技術棧
- **架構**：.NET 10.0 / ASP.NET Core Razor Pages / EF Core / xUnit
- **執行指令**：`DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj /p:EnableSourceControlManagerQueries=false`

## 驗證結果
- 總測試數：38 項
- 通過：38 項 (Passed)
- 失敗：0 項
- 略過：0 項

## 涵蓋重點
1. **過去課堂（如 10/19）過濾與防止回退選取**：新增 `PastSlots_AreFilteredOut_And_SelectedDateDoesNotFallbackToPast`，確保今天以前的過期課堂不會載入，且當本週無課堂時不會回退選取舊日期。
2. **7 日導覽與課堂類型徽章**：驗證往前只到今天、往後不限與徽章聚合顯示。
3. **既有全系統業務流程**：37 項既有測試全數通過。
