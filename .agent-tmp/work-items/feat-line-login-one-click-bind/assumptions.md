# 假設與技術設計 (assumptions.md)

## 1. LINE Login OAuth 2.1 授權綁定架構
- **環境變數與組態支援**：
  - `LineLogin__ClientId` 或 `LINE_LOGIN_CLIENT_ID` 或 `LineBot__ChannelId`：LINE Login Channel ID
  - `LineLogin__ClientSecret` 或 `LINE_LOGIN_CLIENT_SECRET`：LINE Login Channel Secret
  - `LineLogin__RedirectUri` 或由請求動態組出：預設 callback 路徑為 `/LineAuth/Callback`
- **CSRF 防護與安全機制**：
  - 發起授權跳轉時，產生加密/安全的 `state` 參數並暫存在 Cookie 或 Session 中，比對成功才接受回調。
  - 亦可將目前登入家長之 `userId` 驗證資訊綁定於 state 或當前登入 Cookie，確保回調綁定時僅能綁定當前登入家長。
- **OAuth Callback 流程**：
  1. 家長點擊「LINE 一鍵自動授權綁定」
  2. 系統跳轉至 `https://access.line.me/oauth2/v2.1/authorize?response_type=code&client_id={ClientId}&redirect_uri={RedirectUri}&state={state}&scope=profile%20openid`
  3. 家長於手機 LINE 點擊同意授權，LINE 回跳至 `/LineAuth/Callback?code={code}&state={state}`
  4. 系統後端向 `https://api.line.me/oauth2/v2.1/token` 換取 `id_token` / `access_token`
  5. 解析出家長的真實 LINE User ID (`sub` 或呼叫 `/v2/profile`)
  6. 將 `LineUserId` 寫入當前家長帳號之 `BK_Users.LineUserId`
  7. 跳回 `/Parent` 頁面並提示「LINE 通知帳號已成功綁定！」
- **退避與開發友善模式 (Mock / Config Missing)**：
  - 若在開發環境或尚未設定 `LineLogin__ClientId` 時，系統給予友善提示，或提供模擬綁定機制，不破壞測試流程與單元測試。
