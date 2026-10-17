# Plan

## 修復順序

1. 修正 `scripts/notify-pm.js` 的 CLI 參數讀取與缺參錯誤處理。
2. 新增 `scripts/validate-work-item.js`，集中驗證工作單 manifest、必要報告、schema 非空/可解析、references 非空。
3. 新增 `scripts/check-secrets.js`，掃描工作單 Markdown 與專案檔案中的高風險敏感資訊樣式。
4. 更新 `package.json` scripts 與 `.github/workflows/gate-check.yml`，讓 CI 執行 `npm run validate` 與 `npm run check-secrets`。
5. 補齊 schema/checklist 最小有效內容。
6. 執行本機驗證並產出 `TEST_REPORT.md`。

## 風險控管

- 不加入額外 npm dependency，降低 template 複雜度。
- schema 只補骨架與範例，不推測正式業務欄位。
- secret scan 採保守規則並允許常見 placeholder，降低範例檔誤判。
