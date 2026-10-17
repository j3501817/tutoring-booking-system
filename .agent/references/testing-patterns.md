# 🧪 Testing Patterns & Playbook

本指南定義了本專案的自動化測試標準。所有 Agent (Worker/Reviewer) 在執行 `test-driven-development` 或 `VERIFY Gate` 時，必須嚴格遵守此規範。

## 1. 核心測試哲學
- **測試金字塔 (80/15/5)**：80% 單元測試 (Unit Tests)、15% 整合測試 (Integration Tests)、5% 端到端測試 (E2E Tests)。
- **The Beyoncé Rule**："If you liked it, then you should have put a test on it." (任何你認為重要的業務邏輯，都必須有測試保護)。
- **DAMP over DRY**：在測試程式碼中，可讀性與描述性 (Descriptive And Meaningful Phrases) 優先於避免重複 (Don't Repeat Yourself)。每個測試案例應該要能獨立閱讀與理解。

## 2. 專案標準技術棧
- **後端 (API/Service)**：一律使用 `Jest` 與 `Supertest`。
- **前端 (UI/Component)**：一律使用 `React Testing Library` 與 `Jest`。
- **端到端 (E2E)**：一律使用 `Playwright`。

## 3. 結構與命名規範
- 測試檔案命名必須與被測試檔案同名，並加上 `.test.ts` 或 `.spec.ts` 結尾。
- 測試案例必須遵循 `Arrange-Act-Assert (AAA)` 模式，並在程式碼中以註解標明這三個區塊。
- `describe` 區塊應描述被測試的 Function 或 Component 名稱。
- `it` / `test` 區塊必須以動詞開頭，清楚說明預期行為（例如：`it('should return 400 when email is invalid')`）。

## 4. 🛑 強制 Mock 策略 (Mocking Strategy)
**【絕對禁令】**：在單元與整合測試中，**絕對禁止**對外部真實服務（如 LINE API、AWS S3、真實金流 Endpoint）發起真實網路請求！
- 所有外部 API 呼叫必須使用 `jest.mock()` 或依賴注入的方式進行攔截與替換。
- 資料庫測試應使用 In-Memory DB (如 SQLite memory mode) 或獨立的 Test Database，每次測試前後必須清空資料 (`beforeEach`/`afterEach`)。

## 5. 測試反模式 (Anti-Patterns to Avoid)
- ❌ **測試實作細節**：不應測試 Private methods，應從 Public interface 驗證行為。
- ❌ **互相依賴的測試**：測試案例的執行順序不應影響結果。禁止測試間共享未清理的狀態 (Shared State)。
- ❌ **沒有 Assert 的測試**：只是跑過程式碼但不驗證結果的假測試。