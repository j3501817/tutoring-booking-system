# Agentic Project Template

## Governance Flow Improvements

Current governance improvements are documented in:

- [docs/template-adoption.md](docs/template-adoption.md)
- [docs/governance-flow-improvements.md](docs/governance-flow-improvements.md)
- [docs/request-efficiency-routing.md](docs/request-efficiency-routing.md)
- [docs/request-efficiency-usage.md](docs/request-efficiency-usage.md)

Key commands:

```powershell
npm run agentic:profile:refresh
npm run agentic:contract:self-check
npm run agentic:test
```

For new projects, copy the template files without the template `.git` directory and then run `git init`. Do not clone this repository and only change `origin`, because that preserves template Git history. The initialization script blocks known template history by default; see [docs/template-adoption.md](docs/template-adoption.md).

## Request Efficiency Workflow

本樣版支援 project profile cache、Issue completeness preflight、context pack、validation env bootstrap、governance artifact skeleton、routing gates 與 per-issue execution budget guard。

常用指令：

```powershell
npm run agentic:profile:refresh
.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\path\to\issues.json -MaxParallel 2
.\scripts\codex-batch-issues.ps1 -MaxParallel 2
```

詳細使用方式請看 [docs/request-efficiency-usage.md](docs/request-efficiency-usage.md)。

這是一套給團隊使用的本機 Agentic 開發範本。它把 GitHub Issue、Codex CLI、本機 `git worktree`、工作單紀錄、驗證報告與 Pull Request 流程整合在一起，讓團隊可以用一致的方式處理需求與修補。

核心原則：

- 支援多語言、多框架專案，不綁定單一技術棧。
- 新專案必須用不繼承 template Git history 的方式建立，再進行少量設定。
- Codex 在本機執行，GitHub Actions 只做 CI 驗證。
- 不使用 OpenAI API key，不額外產生 API 費用。
- 每張 GitHub Issue 對應一個 work item、一個 worktree、一個 branch、一個 PR。
- 所有 AI 工作紀錄集中在 `.agent-tmp/work-items/<work-item-id>/`。

## 適用對象

- PM：建立 Issue、提供驗收條件、審查 PR。
- Tech Lead / Reviewer：確認範圍、風險、驗證結果與 merge 決策。
- Developer：維護 template、補充專案特定驗證指令與文件。
- Codex Operator：在本機執行批次 Issue 處理腳本。

## 先決條件

請在本機安裝並完成登入：

- `git`
- GitHub CLI：`gh auth login`
- `node`
- Codex CLI，使用 ChatGPT 帳號登入

此範本的 PowerShell 腳本會在執行 Git/GitHub 命令前，自動清除 process-level `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 與小寫 variants，避免 Codex shell 繼承不可用 proxy，例如 `127.0.0.1:9`。一般使用者不需要每次手動清 proxy。

## 使用此 Template 的完整流程

本節是團隊建立新系統時的標準流程。目標是取得此 template 的檔案、治理規則與 automation，但不把 template 原始作者的 Git history 帶進新系統。

### 0. 選擇採用方式

首選方式是 GitHub `Use this template`。GitHub 官方文件說明，從 template 建立 repository 會以單一 commit 開始；fork 則會包含 parent repository 的完整 commit history。

不要使用以下流程：

```powershell
git clone https://github.com/<template-owner>/<template-repo>.git <new-project>
cd <new-project>
git remote set-url origin https://github.com/<owner>/<new-repo>.git
git push -u origin main
```

這會把 template repo 的既有 commit history 帶到新 repository。

### 1. 建立不繼承 Git history 的新 repository

首選流程：

1. 在 template repository 頁面點選 `Use this template`。
2. 選擇 `Create a new repository`。
3. 使用新系統的 owner、repository name 與 visibility。
4. 建立後 clone 新 repository，而不是 clone template repository。
5. 確認新 repository 的第一個 commit 是新專案初始化 commit。

替代流程：

1. 下載 template ZIP，或複製檔案但排除 `.git/`。
2. 在新專案目錄初始化 Git：

```powershell
git init
git branch -M main
git remote add origin https://github.com/<owner>/<new-repo>.git
git add .
git commit -m "Initial commit"
git push -u origin main
```

既有產品 repository 導入此 template 時，可以保留產品既有 history，但初始化時必須明確使用 `-AllowExistingHistory`。

詳細流程請看 [docs/template-adoption.md](docs/template-adoption.md)。

### 2. 安裝本機工具並登入

確認本機已安裝並完成登入：

```powershell
git --version
gh auth status
node --version
codex --version
```

若 `gh auth status` 失敗，先執行：

```powershell
gh auth login
gh auth setup-git
```

Codex CLI 應使用 ChatGPT 帳號登入，不需要 OpenAI API key。

### 3. 確認 remote 指向新 repository

在新 repository 的本機目錄執行：

```powershell
git remote -v
```

`origin` 必須指向新系統 repository，不得指向此 template repository。

### 4. 初始化 template

```powershell
.\scripts\init-codex-template.ps1
```

初始化會做下列檢查與設定：

- 檢查 `git`、`gh`、`node`、`codex` 是否可用。
- 檢查 Git 採用狀態，避免保留 template Git history。
- 檢查 GitHub CLI auth 與 remote 存取。
- 建立或更新 Codex labels。
- 建立 `.agent-tmp/work-items`。
- 產生 `.agent-tmp/project-profile.json`。
- 補上必要 `.gitignore` 設定。

若你是在既有產品 repository 導入 template：

```powershell
.\scripts\init-codex-template.ps1 -AllowExistingHistory
```

若你是在維護 template repository 本身：

```powershell
.\scripts\init-codex-template.ps1 -AllowTemplateRepository -AllowExistingHistory
```

### 5. 設定專案驗證策略

檢查 [agentic.config.json](agentic.config.json)：

- `governance` 是 template 自身的治理工具設定。
- `projectProfile.safeValidationCommands` 是 Codex 預設可安全執行的驗證指令。
- `applicationChecks.enabled` 預設為 `false`；若新系統已有明確測試指令，可改為 `true` 並設定對應 profile。
- 需要外部服務、資料庫、連線或 e2e 的測試，應放在 opt-in，不要讓 Codex 預設執行。

修改後重新產生 profile：

```powershell
npm run agentic:profile:refresh
```

### 6. 執行 Git/GitHub preflight

```powershell
. .\scripts\git-github-preflight.ps1
Test-AgenticGitHubPreflight -GhCommand gh -RemoteName origin
```

此步驟用來確認 GitHub CLI、Git remote 與目前 shell 環境可用。腳本會清除本次 PowerShell process 的 proxy 變數，不會修改機器層級設定。

### 7. 建立可交給 Codex 的 GitHub Issue

Issue 至少要包含：

- 背景與目標
- 驗收條件
- 不做什麼
- 影響範圍
- 已知限制或風險

建議格式：

```markdown
## 背景

## 目標

## 驗收條件
-

## 不做什麼
-

## 影響範圍
- frontend/backend/infra/test/shared

## 風險
- 是否涉及 auth、payment、security、DB migration、production、infra、secrets、data deletion？
```

若需求涉及高風險範圍，加上：

```text
risk:high
```

高風險 Issue 不會自動實作，會轉入 PM Review。

### 8. 加上 automation label

只對可自動處理的 open Issue 加上：

```text
codex:auto-fix
```

### 9. 先執行 dry run

Dry run 只驗證 Issue 路由、工作單 artifact 與高風險判斷，不會執行 Codex、不會改 GitHub label、不會 commit、不會 push、不會建立 PR。

```powershell
.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\path\to\issues.json -MaxParallel 1
```

若要直接從 GitHub 抓取已標記的 Issue：

```powershell
.\scripts\codex-batch-issues.ps1 -DryRun -MaxParallel 1
```

### 10. 執行 Codex Issue automation

```powershell
.\scripts\codex-batch-issues.ps1 -MaxParallel 1
```

初期建議使用 `-MaxParallel 1`，等團隊熟悉流程且 PR 品質穩定後，再提高並行數。

每張 Issue 會建立：

- Work item：`issue-<number>`
- Branch：`codex/issue-<number>`
- Worktree：`<repo-name>-issue-<number>`
- Sandbox：`.agent-tmp/work-items/issue-<number>/`
- Pull Request：`Fix #<number>: <issue title>`

### 11. Review PR 與工作單報告

PR 建立後，Reviewer 至少檢查：

- PR diff 是否只處理該 Issue。
- `.agent-tmp/work-items/<id>/TEST_REPORT.md`。
- `.agent-tmp/work-items/<id>/DELIVERY_REPORT.md` 或 `PM_Review.md`。
- GitHub Actions CI 結果。
- 是否有 secrets、production config、infra 或 DB migration 等未授權變更。

CI 未通過前不要 merge。若需要修正，應在同一個 issue branch 上處理後重新 push。

### 12. 日常維護

- 需求不清楚時，先補 Issue 或 `docs/00-Intent-Spec.md`，不要讓 Codex 猜測。
- 專案技術棧、測試指令或必要環境變數變更後，更新 `agentic.config.json` 並執行 `npm run agentic:profile:refresh`。
- 定期執行 `npm run agentic:test` 檢查 template governance。
- 多 Issue 並行前，先確認本機資源、CI 時間與 review 品質可承受。

## 新專案快速開始

1. 使用 GitHub `Use this template` 建立新 repository。
2. Clone 新 repository 到本機，確認 `git remote -v` 指向新 repository。
3. 執行 `.\scripts\init-codex-template.ps1`。
4. 執行 Git/GitHub preflight。
5. 建立 Issue，補齊驗收條件與風險。
6. 加上 `codex:auto-fix`。
7. 先跑 dry run，再跑 live automation。
8. Review PR、CI 與工作單報告後再 merge。

## Dry Run

Dry run 可驗證路由、工作單 artifact 與高風險判斷，不會執行 Codex、不會改 GitHub label、不會 commit、不會 push、不會建立 PR。

```powershell
.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\path\to\issues.json -MaxParallel 1
```

Issue JSON 可以是一個物件或陣列，欄位至少包含：

```json
{
  "number": 123,
  "title": "修正使用者資料驗證",
  "body": "背景、需求、驗收條件...",
  "url": "https://github.com/org/repo/issues/123",
  "labels": [{ "name": "codex:auto-fix" }]
}
```

Dry-run artifact 會寫入：

```text
.agent-tmp/work-items/dry-run-issue-<number>/
```

## 標準工作流程

1. PM 建立 GitHub Issue。
2. PM 或 Tech Lead 補足驗收條件與風險標示。
3. 對可自動處理的 Issue 加上 `codex:auto-fix`。
4. Operator 執行 `scripts/codex-batch-issues.ps1`。
5. 腳本建立 worktree、branch、work item sandbox。
6. Codex CLI 在本機處理單一 Issue。
7. 腳本 commit、push branch、建立 PR。
8. GitHub Actions 執行 CI 驗證。
9. Reviewer 檢查 PR、`TEST_REPORT.md`、`DELIVERY_REPORT.md`。
10. PM 或 Reviewer 決定 merge、要求修改或退回人工處理。

## Issue 與分支對應

GitHub Issue `#123` 會對應到：

- Work item：`issue-123`
- Branch：`codex/issue-123`
- Worktree：`<repo-name>-issue-123`
- Sandbox：`.agent-tmp/work-items/issue-123/`
- PR title：`Fix #123: <issue title>`

每張 Issue 必須獨立，不共用 branch，不共用 worktree，不跨 Issue 擴大修改範圍。

## 工作單 artifact

每個 work item 至少要有：

- `status.md`
- `assumptions.md`
- `TEST_REPORT.md`
- `DELIVERY_REPORT.md` 或 `PM_Review.md`

GitHub Issue 型工作單還會保存：

- `issue.json`
- `issue.md`

`status.md` 必須使用 emoji 狀態，不使用 `[x]` checklist：

- `⬜️` 待處理
- `🚧` 進行中
- `✅` 已完成
- `❌` 卡關或錯誤

## 高風險與人工審查

以下類型預設不自動實作，會轉入 PM Review：

- authentication / authorization
- payment / billing
- security / secrets
- database migration
- production deployment
- infrastructure / Terraform / Kubernetes
- permission model
- data deletion
- destructive operation

可以直接加上 label：

```text
risk:high
```

腳本會移除 `codex:auto-fix` 並加上：

```text
codex:manual-review
```

## GitHub Labels

初始化腳本會建立或更新以下 labels：

- `codex:auto-fix`：準備進入本機 Codex automation
- `codex:working`：處理中
- `codex:pr-created`：PR 已建立
- `codex:failed`：自動化失敗
- `codex:manual-review`：需要人工審查
- `risk:high`：高風險，不自動實作
- `area:frontend`
- `area:backend`
- `area:infra`
- `area:test`

## 驗證策略

Agent 會先偵測專案技術棧，再選擇最小必要驗證：

- 專案明確指令優先，例如 `package.json`、`Makefile`、`Taskfile.yml`、`justfile`、README 或 CI workflow。
- 沒有明確指令時，依 `AGENTS.md` 的多語言驗證規則選擇預設指令。
- 驗證結果必須寫入 `.agent-tmp/work-items/<id>/TEST_REPORT.md`。
- 不得為了通過測試而刪除測試、降低檢查標準或移除驗證步驟。

本 template 自身的治理檢查：

```powershell
npm run agentic:test
```

## Git/GitHub Preflight

共用 helper：

```powershell
.\scripts\git-github-preflight.ps1
```

手動檢查：

```powershell
. .\scripts\git-github-preflight.ps1
Test-AgenticGitHubPreflight -GhCommand gh -RemoteName origin
```

它會：

- 清除本次 PowerShell process 的 proxy 變數。
- 設定非互動模式，避免自動化卡住。
- 檢查 `gh auth status`。
- 檢查 `git ls-remote origin HEAD`。

這是為了降低人工作業，不是修改使用者或機器層級的環境變數。

## CI 定位

GitHub Actions 只做 CI 驗證，不在雲端執行 Codex。

本機 Codex 負責：

- 讀取 Issue
- 修改程式
- 產生測試與交付報告
- 建立 branch 與 PR

GitHub Actions 負責：

- governance validation
- secret scan
- project validation，依偵測到的技術棧執行必要驗證
- 其他團隊自行補上的 CI jobs

PR checks 預設只維持穩定名稱：

- `Agentic governance`
- `Project validation`

未偵測到的語言或框架不會各自顯示成 skipped check，避免被誤解為未完成事項。實際偵測到與未偵測到的 stack 會寫入 `Project validation` 的 GitHub Step Summary。

## 常見問題

### Codex shell 中 `git ls-remote` 失敗

請優先跑：

```powershell
. .\scripts\git-github-preflight.ps1
Test-AgenticGitHubPreflight -GhCommand gh -RemoteName origin
```

若此命令成功，請透過 template 腳本操作，不要直接手動呼叫 raw Git/GitHub 命令。

### `gh auth status` 失敗

重新登入：

```powershell
gh auth login
gh auth setup-git
```

再執行 preflight。

### Issue 沒有被處理

請確認：

- Issue 是 open 狀態。
- 有 `codex:auto-fix` label。
- Issue body 足夠描述需求與驗收條件。
- 沒有 `risk:high` 或高風險關鍵字。

### PR 建立後 CI 失敗

不要 merge。請在同一個 issue branch 修正後 push，讓 PR 重新跑 CI。

## 重要文件

- [AGENTS.md](AGENTS.md)：Agent 執行規則與交付標準。
- [docs/auto-fix-flow.md](docs/auto-fix-flow.md)：本機 Codex auto-fix 流程細節。
- [docs/template-adoption.md](docs/template-adoption.md)：新專案採用 template 且不繼承原 Git history 的流程。
- [docs/00-Intent-Spec.md](docs/00-Intent-Spec.md)：需求規格範本。
- [docs/DUAL-SPEC-UPGRADE.md](docs/DUAL-SPEC-UPGRADE.md)：Dual-Spec 使用指引。
- [agentic.config.json](agentic.config.json)：template governance 與 app checks 設定。
