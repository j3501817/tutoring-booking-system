# 測試與驗證報告 (TEST_REPORT.md)

- **工作單編號**：`feature-comprehensive-enhancements`
- **測試日期**：2026-10-24
- **技術棧**：.NET 10.0 (C# / Razor Pages / SQLite / Entity Framework Core)

## 選擇驗證指令理由
本專案為 ASP.NET Core Razor Pages 應用程式，使用 .NET 10 SDK 與 xUnit 測試套件。
依據 `AGENTS.md` 規範，優先執行：
1. `dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj`
2. `dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj`

## 執行的指令與結果

| 指令 | 目的 | 結果 |
|---|---|---|
| `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj` | 主網站專案編譯 | ✅ 通過 (0 錯誤, 0 警告) |
| `DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj` | 單元與整合測試 | ✅ 通過 (20 / 20 通過, 0 失敗, 0 略過) |

## 關鍵測試案例涵蓋清單
1. **`StudentParentLinkingService_BindsBidirectionally_ByPhones`**
   - 學生 Phone1~Phone3 與家長 Phone 雙向自動關聯。
   - 學生先輸入電話，後續家長建立帳號輸入相同電話自動綁定。
   - 家長先建立帳號，後續學生輸入電話自動綁定。
2. **`BookingService_RejectsBooking_Within30MinutesOfStartTime`**
   - 驗證距離開課未滿 30 分鐘之時段預約請求，後端拋出 `InvalidOperationException` 並阻止佔位。
3. **`BookingService_RejectsBooking_OnAcademicHoliday`**
   - 驗證機構排定為休假日之時段，後端拋出例外並阻止預約。
4. **`PriorityDeduction_UsesBonusFirst_ThenPaid_AndReconcilesCleanly`**
   - 扣抵順序：學期贈送堂數優先於付費儲值堂數。
5. **`Cancellation24HourBoundary_RefundsWhenAtLeast24Hours_NoRefundWhenUnder24Hours`**
   - 24 小時以上取消退還堂數，未滿 24 小時不退。
6. **`Waitlist_Hold30Minutes_AndRelease_WhenExpired`**
   - 候補釋出 30 分鐘保留與轉正。
7. **`LineAuthAndBindingTests`**
   - 一鍵授權綁定與 State 防偽保護。
