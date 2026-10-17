# Test Report

## Scope

本次修復將 template 的 Node/npm 使用語意調整為 Agentic governance tooling，不再暗示業務專案必須使用 Node.js。應用程式語言檢查改由 `agentic.config.json` 控制，預設停用。

## Changes Verified

- `package.json` 已改用 `agentic:*` scripts。
- `.github/workflows/gate-check.yml` 已區分 Agentic governance checks 與 optional application checks。
- `agentic.config.json` 已新增 Python、Node.js、.NET C# 的可選檢查 profile。
- `scripts/run-app-checks.js` 已新增，預設在 `applicationChecks.enabled=false` 時跳過語言特定檢查。
- `scripts/validate-work-item.js` 已新增 `agentic.config.json` 結構檢查。

## Verification Log

- `npm.cmd run agentic:lint`：通過。
- `npm.cmd run agentic:app-checks`：通過，依設定跳過語言特定檢查。
- `npm.cmd install --package-lock-only --ignore-scripts`：通過，同步 `package-lock.json` 的治理工具 metadata。
- `npm.cmd run agentic:validate:changed`：通過。本機 Node child_process 無法 spawn git 時會 fallback 至全量 manifest 工作單驗證；GitHub Actions 仍維持無 changed work item 即失敗。
- `npm.cmd run agentic:validate`：通過，驗證 3 個 manifest-backed 工作單。
- `npm.cmd run agentic:check-secrets`：通過。
- `npm.cmd run agentic:test`：通過，執行 governance validate、secret scan、optional app checks。
- `npm.cmd audit --audit-level=high`：通過，0 vulnerabilities。

## Remaining Risk

- `applicationChecks.enabled` 預設為 false；套用到實際 Python / Node.js / .NET C# 專案時，PM 或團隊需在 `agentic.config.json` 啟用對應 profile 與測試命令。
