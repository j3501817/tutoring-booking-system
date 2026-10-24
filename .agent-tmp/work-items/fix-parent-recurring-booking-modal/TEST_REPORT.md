# 測試與驗證報告 (TEST_REPORT.md)

- **工作單編號**：`fix-parent-recurring-booking-modal`
- **測試日期**：2026-10-24
- **技術棧**：.NET 10.0 (C# / Razor Pages / Bootstrap 5 / xUnit)

## 選擇驗證指令理由
本專案為 ASP.NET Core Razor Pages 應用程式，使用 .NET 10 SDK 與 xUnit 測試套件。
執行建置與全套單元測試。

## 執行的指令與結果

| 指令 | 目的 | 結果 |
|---|---|---|
| `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj` | 主網站專案編譯 | ✅ 通過 (0 錯誤, 0 警告) |
| `DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj` | 單元與整合測試 | ✅ 通過 (22 / 22 通過, 0 失敗, 0 略過) |

## 驗證項目
1. `Parent/Index.cshtml` 週期預約按鈕具備 `data-bs-toggle="modal"`、`data-bs-target="#modalRecurringBooking"` 與 `onclick="openRecurringBookingModal()"` 雙重觸發。
2. 全域函式 `openRecurringBookingModal` 正確定義並掛載至 `window`。
3. Modal 具備未選擇學生之防呆提示與關閉按鈕。
