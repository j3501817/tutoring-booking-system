# Test Report

## Scope

本次修復強化 agentic template 的通知、CI、工作單驗證、schema/checklist 與敏感資訊掃描。

## Verification Log

- `npm.cmd run lint`：通過，確認 `scripts/notify-pm.js`、`scripts/validate-work-item.js`、`scripts/check-secrets.js` 語法有效。
- `node scripts\notify-pm.js hardening-fixes-20260508-1632`：通過，在 `NOTIFICATION_STRATEGY=NONE` 時不發送通知，且工作單 id 可正確進入訊息內容。
- `npm.cmd run check-secrets`：第一次失敗，原因為 `.agent/skills` 內教學範例造成誤判；已調整掃描範圍排除 `.agent/skills` 並保留 `.agent/references`。
- `npm.cmd run check-secrets`：修正後通過。
- `npm.cmd run validate`：通過，驗證 `architecture-review-20260508-1157` 與 `hardening-fixes-20260508-1632` 工作單產物。後續已改名為 `npm run agentic:validate`。
- `npm.cmd test`：通過，執行 `validate` 與 `check-secrets`。後續已改名為 `npm run agentic:test`，避免和應用程式測試混淆。
- `npm.cmd audit --audit-level=high`：通過，0 vulnerabilities。
- `node scripts\notify-pm.js`：缺少工作單 id 時如預期失敗，回報正確用法。

## Remaining Risk

- `validate:changed` 在本機未 staged/untracked 狀態無法完整模擬 GitHub PR diff；CI 已設定 checkout `fetch-depth: 0`，正式 PR 會以 changed work item 驗證。
