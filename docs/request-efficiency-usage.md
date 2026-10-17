# Request Efficiency Workflow Usage

Taiwan time checked: 2026-05-28 14:58:29 +08:00

## 目的

本文件說明 `agentic-project-template` 的 request-efficiency workflow。目標是把可決定的治理工作交給 wrapper 腳本處理，避免 Codex 在每張 Issue 重複偵測專案語言、驗證指令與 artifact 規則。

## 前置條件

- 已安裝 `git`、GitHub CLI `gh`、`node`、Codex CLI。
- GitHub CLI 已登入並可存取 repository。
- 要自動處理的 Issue 已加上 `codex:auto-fix` label。
- Issue body 需包含清楚目標、驗收條件或預期行為、影響範圍、風險說明與驗證期待。
- 高風險 Issue 應加上 `risk:high`，並由 routing gate 轉入 PM review。

## 初始化

第一次採用 template 時執行：

```powershell
.\scripts\init-codex-template.ps1
```

初始化會建立 `.agent-tmp/work-items`、產生 project profile，並執行 artifact contract self-check：

```text
.agent-tmp/project-profile.json
temporary dry-run-contract-self-check work item outside the repository
```

手動刷新 project profile：

```powershell
npm run agentic:profile:refresh
```

手動執行 artifact contract self-check：

```powershell
npm run agentic:contract:self-check
```

## Project Profile

可在 `agentic.config.json` 設定：

```json
{
  "projectProfile": {
    "path": ".agent-tmp/project-profile.json",
    "primaryLanguage": "unknown",
    "applicationType": "unknown",
    "safeValidationCommands": ["npm run agentic:test"],
    "focusedValidationCommands": [],
    "requiredEnvPlaceholders": {},
    "optInTestMarkers": ["connectivity", "e2e"]
  }
}
```

`requiredEnvPlaceholders` 只可放本機驗證用 placeholder，不得放 production secret。

## Dry Run

不啟動 Codex 時，可用 dry-run 檢查 routing gate 與 skeleton artifact：

```powershell
.\scripts\codex-batch-issues.ps1 `
  -DryRun `
  -IssueJsonPath .\path\to\issues.json `
  -MaxParallel 2
```

Dry-run 會：

- 載入 project profile。
- 檢查 Issue 完整性、mojibake 與高風險條件。
- 建立 work item skeleton。
- 產生 `CONTEXT.md`。
- 執行 work item artifact contract validation。
- 略過 Codex、commit、push、PR 與 GitHub label mutation。

## 執行

一般執行：

```powershell
.\scripts\codex-batch-issues.ps1 -MaxParallel 2
```

指定 label：

```powershell
.\scripts\codex-batch-issues.ps1 -Label "codex:auto-fix" -MaxParallel 2
```

調整 Issue body 最小長度：

```powershell
.\scripts\codex-batch-issues.ps1 -MinimumIssueBodyLength 120
```

調整每張 Issue 的 retry 預算：

```powershell
.\scripts\codex-batch-issues.ps1 -MaxRetries 1
```

## Routing Gates

`MaxParallel` 保留作為 Codex job 並行上限。wrapper 會先跑便宜的 routing gate，再把 ready work item 放入 Codex queue。route key 來源包含：

- `area:*` labels。
- auth / permission / RBAC。
- database / migration / schema。
- CI / workflow / deployment。
- infra / terraform / kubernetes / docker。
- shared / common / core / template。

若 operator 已人工確認可接受衝突風險，可覆寫 routing gate：

```powershell
.\scripts\codex-batch-issues.ps1 -MaxParallel 5 -OverrideRoutingGates
```

## Manual Review

以下狀況不會啟動 Codex，會產生 `PM_Review.md`：

- Issue body 空白或過短。
- Issue 內容疑似 mojibake 或 replacement character。
- 缺少清楚 requested change。
- 缺少驗收條件、預期行為或影響範圍。
- 命中高風險 keyword 或 `risk:high` label。

預設情況下，preflight 失敗的 Issue 不會建立 implementation branch。

## Codex Prompt

wrapper 產生的 prompt 會要求 Codex 先讀：

- `AGENTS.md`
- `.agent-tmp/project-profile.json`
- `<work-item>/issue.md`
- `<work-item>/CONTEXT.md`

Codex 應優先使用 project profile 與 context pack，不應在每張 Issue 重複偵測專案語言、框架或 validation commands。若 profile/context 不足，需先把缺口寫入 `assumptions.md`。

## 驗證

template 的治理驗證：

```powershell
npm run agentic:lint
npm run agentic:test
```

刷新 project profile：

```powershell
npm run agentic:profile:refresh
```

## Pre-PR CI-Parity Validation

Codex 完成後，wrapper 會在 push 與 PR 前執行：

```powershell
node scripts/validate-work-item.js --work-item <work-item-id>
node scripts/check-secrets.js
node scripts/run-app-checks.js
```

任何一個步驟失敗都不會建立 PR，並會把失敗摘要寫入 `TEST_REPORT.md` 與 `PM_Review.md`。

PR 建立後，wrapper 會用 GitHub CLI 讀取 check status，並寫入 `DELIVERY_REPORT.md`。

## 交付檢查

請確認：

- `CONTEXT.md` 存在於 work item sandbox。
- `work-item.json` 包含 `queueState`、`retryCount`、`maxRetries`、`requiredArtifacts` 與 `completionArtifactsOneOf`。
- `TEST_REPORT.md` 記錄實際執行的驗證指令與結果。
- `DELIVERY_REPORT.md` 或 `PM_Review.md` 存在且非空。
- artifact 中沒有 production secret。
