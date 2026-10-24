# 測試與驗證報告 (TEST_REPORT.md)

## 1. 偵測技術棧與驗證指令
- 技術棧：.NET 10 (C# / ASP.NET Core Razor Pages / xUnit)
- 選擇理由：專案為 C# ASP.NET Core Web 與 xUnit 測試專案。
- 執行驗證指令：
  - `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore -p:EnableSourceControlManagerQueries=false`
  - `DOTNET_CLI_HOME=/tmp dotnet test tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj --no-restore`

## 2. 驗證結果
- **專案建置**：通過（0 個錯誤，1 個已知 SQLitePCLRaw 弱點警告）
- **單元測試結果**：
  - 總計測試案例：15
  - 通過：15
  - 失敗：0
  - 略過：0
  - 執行時間：718 ms

## 3. LINE 授權與通知測試案例覆蓋
- `BuildAuthorizeUrl_WhenConfigured_GeneratesValidOAuthUrl`：驗證 LINE OAuth 2.1 授權跳轉網址參數正確包含 client_id, state, redirect_uri, scope。
- `BuildAuthorizeUrl_WhenNotConfigured_ThrowsInvalidOperationException`：驗證未設定憑證時的安全攔截與友善錯誤提示。
- `ExchangeCodeForProfileAsync_WithValidMock_ReturnsUserProfile`：驗證授權 code 換取 token 與 profile 取得 LINE User ID 及顯示名稱之完整流程。
- `SendPushMessageAsync_WhenTokenConfigured_CallsLineApiSuccessfully`：驗證真實 LINE Messaging API 呼叫格式、Bearer Token、JSON 編碼。
- `ParentUnbindFlow_ClearsLineUserId`：驗證解除綁定時正確清除 `LineUserId`。
- 其他既有測試（排程、點名、預約代約、流水帳結算）全部 100% 通過。
