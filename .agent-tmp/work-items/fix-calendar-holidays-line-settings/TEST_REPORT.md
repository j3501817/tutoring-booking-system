# 測試與驗證報告 (TEST_REPORT.md)

- **工作單編號**：`fix-calendar-holidays-line-settings`
- **測試日期**：2026-10-24
- **技術棧**：.NET 10.0 (C# / Razor Pages / SQLite / EF Core)

## 選擇驗證指令理由
本專案為 ASP.NET Core Razor Pages 應用程式，使用 .NET 10 SDK 與 xUnit 測試套件。
依據規範執行：
1. `dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj`
2. `dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj`

## 執行的指令與結果

| 指令 | 目的 | 結果 |
|---|---|---|
| `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj` | 主網站專案編譯 | ✅ 通過 (0 錯誤, 0 警告) |
| `DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj` | 單元與整合測試 | ✅ 通過 (22 / 22 通過, 0 失敗, 0 略過) |

## 關鍵測試案例涵蓋清單
1. **`LineAuthService_DetectsMessagingApi_AndReadsSettings`**
   - 驗證當環境僅配置 `LineBot:ChannelAccessToken` 與 `LineBot:ChannelSecret` 時，系統精準識別 Messaging API 就緒。
   - 驗證 Channel ID 可由資料庫 `SystemSettings` 動態回退讀取。
2. **`SystemAdminCalendar_BuildsMonthlyGrid_WithHolidayMap`**
   - 驗證月曆視圖之年月日、起首星期、當月天數與休假日字典對照表無誤。
3. **`StudentParentLinkingService_BindsBidirectionally_ByPhones`**
   - 學生三支手機與家長電話雙向關聯。
4. **`BookingService_RejectsBooking_Within30MinutesOfStartTime`**
   - 開課前 30 分鐘截止預約。
5. **`BookingService_RejectsBooking_OnAcademicHoliday`**
   - 休假日禁止預約。
