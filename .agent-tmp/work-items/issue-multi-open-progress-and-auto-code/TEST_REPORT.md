# Test Report — issue-multi-open-progress-and-auto-code

- **偵測到的技術棧**：.NET 10 / ASP.NET Core Razor Pages + xUnit
- **選擇驗證指令的理由**：驗證多進度同時開放切換、自動產生進度代碼邏輯與全系統單元測試
- **實際執行的指令**：
  - `dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj /p:EnableSourceControlManagerQueries=false`
- **每個指令的結果**：通過（Passed: 40, Failed: 0, Skipped: 0）
- **略過原因**：無
- **失敗摘要**：無
- **是否已修復**：是
- **是否需要 PM Review**：否
