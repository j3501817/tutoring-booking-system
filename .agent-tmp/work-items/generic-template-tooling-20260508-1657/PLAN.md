# Plan

1. 將 `package.json` scripts 改為 `agentic:*` 命名。
2. 新增 `agentic.config.json` 描述 governance runtime 與 optional application checks。
3. 新增 `scripts/run-app-checks.js`，依設定選擇性執行 Node/Python/.NET 應用程式檢查；預設跳過。
4. 更新 GitHub Actions，先跑 governance checks，再跑 optional application checks。
5. 更新本工作單與前一工作單 expected checks，避免驗證規則失準。
6. 執行 lint、validate、secret scan、audit。
