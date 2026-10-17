# Intent Spec: Agentic Template Hardening Fixes

**Owner: PM** | 最後更新：2026-05-08

## 1. Goal

依已核准的架構審查結論，修補目前 template 的交付流程漏洞，讓通知、CI、工作單、schema 與資安檢查具備最小可驗證能力。

## 2. Acceptance Criteria

- 修正 `scripts/notify-pm.js` 可正確讀取 `<work-item-id>`，缺少 id 時要失敗。
- 新增可由 CI 執行的工作單驗證機制，避免舊 `TEST_REPORT.md` 讓新 PR 偽通過。
- 新增敏感資訊掃描，避免工作單 Markdown 與程式碼誤帶 token/password/key 類字串。
- 補上有效的 schema/checklist 範本，避免 0 bytes 空殼檔案。
- 更新 `package.json` 與 GitHub Actions，讓品質 gate 至少執行一個非 no-op 的 `validate`。
- 產出本工作單 `TEST_REPORT.md`。

## 3. Non-goals

- 不導入完整測試框架，例如 Jest、Playwright。
- 不改變現有 8 段狀態機治理模型。
- 不建立真實 LINE/MPA 通知金鑰或外部服務設定。
- 不處理既有未提交且與本次無關的工作區變更。

## 4. Constraints & Dependencies

- 目前專案只有 Node.js 與 `dotenv` dependency。
- CI 目標為 GitHub Actions pull request gate。
- schema/checklist 只能補最小有效範本，不假設不存在的業務 API 細節。

## 5. Change Contract Mapping

- 觸發器/路由/Function 變更：Y，`notify-pm.js` CLI 參數契約修正。
- 通知策略變更：N，不改通知通道，只修正參數與缺參處理。
- 資料來源或查詢邏輯變更：N。
- 輸出格式或檔名契約變更：Y，新增 `work-item.json` manifest 與 validation gate。
- 環境變數變更：N。

## 6. SDD Gate Check

- docs/00-Intent-Spec.md 相關段落已更新？N，本次為 template hardening，不改全域規格模板。
- 受影響 AC 已同步確認？Y。
- 版本記錄已更新？N/A，本次以工作單紀錄交付。
- 第二份規格文件需同步？N。
- 結論：允許進入 RD，PM 已回覆 Approve。

## 7. Version Log

- v0.1.0 - 建立 hardening 修復工作單 - 2026-05-08 - 作者：Agent
