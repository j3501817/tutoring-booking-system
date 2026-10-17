# 🛡️ Security & Hardening Checklist

本指南定義了本專案的基礎資安標準。所有 Agent 在執行 `security-and-hardening` 技能與進入 `REVIEW Gate` 時，必須逐項核對此清單。若發現違規，必須標示為 `blocker` 級別漏洞，禁止交付。

## 1. 驗證與授權 (Auth & Authorization)
- [ ] **密碼儲存**：密碼絕對不可明文儲存，必須使用 `bcrypt` (至少 10 rounds) 或 `Argon2` 進行 Hash 處理。
- [ ] **JWT 安全性**：JWT 必須設定合理的過期時間 (Expiration)。不應在 Payload 中放置密碼或其他敏感個資。
- [ ] **越權存取防護 (IDOR/BOLA)**：任何操作特定資源（如 `GET /api/users/:id`）的請求，必須在 Controller/Service 層驗證當前登入者是否有權限存取該 ID。

## 2. 輸入驗證與防禦 (Input Validation)
- [ ] **SQL Injection 防禦**：所有資料庫查詢必須使用 ORM (如 Prisma, TypeORM) 或 Parameterized Queries (參數化查詢)。絕對禁止使用字串拼接組成 SQL 語法。
- [ ] **XSS (跨站腳本攻擊) 防禦**：前端渲染使用者輸入時必須跳脫 (Escape) 特殊字元（React 預設已防護，需禁用 `dangerouslySetInnerHTML` 除非有嚴格的 Sanitizer）。
- [ ] **Payload 驗證**：所有進站的 API Request (Body, Query, Params) 必須經過 Schema 驗證 (如 Zod, Joi)，拒絕未定義或型別錯誤的惡意 Payload。

## 3. HTTP 與網路層安全 (Network Security)
- [ ] **CORS 設定**：禁止使用 `Access-Control-Allow-Origin: *`。必須明確指定白名單網域。
- [ ] **安全 Headers**：後端應用程式必須掛載 `Helmet` 中介軟體，隱藏 `X-Powered-By`，並強制設定 XSS 攔截與 HSTS 等基本 Header。
- [ ] **Rate Limiting**：對外公開的 API (特別是登入、註冊、發送驗證碼) 必須實作速率限制，防止暴力破解與 DDoS。

## 4. 🛑 敏感資訊防護 (Secrets Management)
**【絕對禁令】**：絕對禁止將任何 API Key、Database URL、JWT Secret 或憑證硬編碼 (Hardcode) 在原始碼中！
- 所有敏感資訊必須透過 `process.env` 讀取。
- Agent 在發起 Commit 前，必須檢查新增的程式碼中是否意外包含了真實的密鑰字串。

## 5. 依賴套件安全 (Dependency Auditing)
- 引入新的第三方套件前，必須確認該套件的維護狀態與主流普及度，避免引入有已知漏洞 (CVE) 或長期未更新的套件。