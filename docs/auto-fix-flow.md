# 本機 Codex Auto-Fix 流程

## 目的

此 template 讓團隊用 GitHub Issue 驅動本機 Codex 開發流程。Codex 在使用者本機執行，GitHub Actions 只負責 CI 驗證，不在雲端執行 Codex，也不需要 OpenAI API key。

流程定位：

```text
GitHub Issue -> 本機 Codex CLI -> git worktree -> issue branch -> Pull Request -> CI -> 人工 Review
```

## 先決條件

- 新專案已依 [template adoption flow](template-adoption.md) 建立，且沒有繼承此 template 的原始 Git history。
- `git`
- `gh`，並已登入可存取該 repo 的 GitHub 帳號
- `node`
- `codex` CLI，並使用 ChatGPT 帳號登入

初始化：

```powershell
.\scripts\init-codex-template.ps1
```

初始化會檢查必要工具、GitHub CLI auth、建立 labels、確認必要檔案與 `.agent-tmp/work-items` 目錄。

初始化腳本會在建立 labels 前檢查 Git 採用狀態。若偵測到 `origin` 仍指向 template repository、目前 repo 是 shallow clone，或新專案疑似已保留多筆既有 commit history，腳本會停止並要求先修正採用流程。

## Git/GitHub Preflight

## Changed Work Item Validation

`npm run agentic:validate:changed` 會相容尚未建立 `work-item.json` 的 legacy 或 intent work-item 目錄。當變更中的 `.agent-tmp/work-items/<id>/` 缺少 manifest 時，changed validation 會輸出警告，並略過該 work item 僅能依賴 manifest 的交付檢查。

完整驗證仍維持嚴格。未使用 `--changed` 執行 `node scripts/validate-work-item.js --work-item <id>` 時，若缺少 `work-item.json` 仍會失敗。

本 template 提供共用 helper：

```powershell
.\scripts\git-github-preflight.ps1
```

手動檢查方式：

```powershell
. .\scripts\git-github-preflight.ps1
Test-AgenticGitHubPreflight -GhCommand gh -RemoteName origin
```

此 helper 會在目前 PowerShell process 內：

- 清除 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY` 與小寫 variants。
- 設定 `GIT_TERMINAL_PROMPT=0`。
- 設定 `GH_PROMPT_DISABLED=1`。
- 檢查 `gh auth status`。
- 在 live run 檢查 `git ls-remote origin HEAD`。

這可避免 Codex shell 繼承不可用 proxy，例如 `127.0.0.1:9`，讓使用者不需要每次手動清環境變數。

## Trigger

對 open GitHub Issue 加上：

```text
codex:auto-fix
```

批次 runner 只會處理 open 且有該 label 的 Issue：

```powershell
.\scripts\codex-batch-issues.ps1 -MaxParallel 1
```

建議初期使用 `-MaxParallel 1`。多 Issue 並行前，先確認 PR 品質、CI 時間與本機資源都穩定。

## Dry Run

Dry run 用來驗證路由與 artifact 產生，不會：

- 執行 Codex
- 變更 GitHub labels
- commit
- push
- 建立 PR

範例：

```powershell
.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\path\to\issues.json -MaxParallel 1
```

JSON 可為單一 Issue 物件或 Issue 陣列：

```json
{
  "number": 123,
  "title": "修正使用者資料驗證",
  "body": "需求背景、驗收條件與限制...",
  "url": "https://github.com/org/repo/issues/123",
  "labels": [{ "name": "codex:auto-fix" }]
}
```

Dry-run artifacts 會寫入：

```text
.agent-tmp/work-items/dry-run-issue-<number>/
```

## 每張 Issue 的隔離規則

Issue `#123` 對應：

- Work item：`issue-123`
- Branch：`codex/issue-123`
- Worktree：`<repo-name>-issue-123`
- Sandbox：`.agent-tmp/work-items/issue-123/`

禁止：

- 多張 Issue 共用同一個 worktree。
- 多張 Issue 共用同一個 branch。
- 在未被指派的 Issue 範圍內修 unrelated bugs。
- 直接 push main。
- 自動 merge PR。

## Issue Context 保存

每張 Issue 會先保存到：

```text
.agent-tmp/work-items/issue-<number>/issue.json
.agent-tmp/work-items/issue-<number>/issue.md
```

`issue.md` 至少包含：

- Issue number
- Issue title
- Issue body
- Issue URL
- Labels
- Acceptance criteria，若 Issue 內有提供

若 Issue 描述不足以安全實作，runner 會建立 `PM_Review.md` 並停止該 Issue 的自動化。

## Manual Review 條件

以下情況會在 Codex 執行前停止，並加上 `codex:manual-review`：

- Issue body 空白或太短。
- Issue 有 `risk:high` label。
- Issue 內容涉及高風險範圍。

高風險範例：

- authentication / authorization
- payment / billing
- security / secrets
- database migration
- production deployment
- infrastructure / Terraform / Kubernetes
- permission model
- data deletion
- destructive operation

## 失敗處理

自動化失敗時，runner 會寫入：

```text
.agent-tmp/work-items/issue-<number>/PM_Review.md
```

並嘗試：

- 執行 `node scripts/notify-pm.js issue-<number>`。
- 將 Issue label 改為 `codex:failed`。
- commit 並 push failure artifacts，若環境允許。

GitHub/Git preflight 失敗會發生在 Codex 實作前。若 `gh auth status` 或 `git ls-remote origin HEAD` 失敗，請先修復本機 GitHub auth 或網路環境，再重新執行。

## 成功處理

成功時，runner 會：

- commit scoped changes。
- push `codex/issue-<number>`。
- 建立 Pull Request。
- 將 Issue label 從 `codex:working` 改為 `codex:pr-created`。
- 更新 work item 狀態與 delivery report。

PR 仍然需要人工 review 後才能 merge。

## PR Body 內容

PR body 會包含：

- Issue 編號
- Issue URL
- 變更摘要
- 修改檔案摘要
- 測試與驗證結果
- 已知風險
- 是否需要 PM 人工裁定

Reviewer 應至少檢查：

- PR diff 是否超出 Issue scope。
- `.agent-tmp/work-items/<id>/TEST_REPORT.md`。
- `.agent-tmp/work-items/<id>/DELIVERY_REPORT.md` 或 `PM_Review.md`。
- GitHub Actions CI 結果。

PR checks 預設只會顯示穩定的 `Agentic governance` 與 `Project validation`。未使用的語言或框架會列在 `Project validation` 的 Step Summary，不會以 skipped checks 方式出現在 PR checks 清單。

## Labels

初始化腳本會建立：

| Label | 用途 |
|---|---|
| `codex:auto-fix` | 允許本機 Codex automation 處理 |
| `codex:working` | 自動化處理中 |
| `codex:pr-created` | PR 已建立 |
| `codex:failed` | 自動化失敗 |
| `codex:manual-review` | 需要人工審查 |
| `risk:high` | 高風險，禁止自動實作 |
| `area:frontend` | 前端範圍 |
| `area:backend` | 後端範圍 |
| `area:infra` | 基礎設施範圍 |
| `area:test` | 測試範圍 |

## 建議 Issue 範本

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

## 殘餘風險與處置

| 風險 | 原因 | 處置方式 |
|---|---|---|
| Codex 可能產生錯誤或不完整程式碼 | wrapper 可限制範圍與要求報告，但無法證明所有語意正確性 | PR 必須人工 review，CI 必須通過，merge 前檢查 `TEST_REPORT.md` 與 `DELIVERY_REPORT.md` |
| CI 可能在 PR 建立後失敗 | 本機環境與 GitHub Actions runner 可能不同 | CI 未通過前不得 merge，修正後 push 到同一 issue branch |
| Issue 需求可能語意不足 | 長度檢查無法理解所有產品意圖 | PM 補充 acceptance criteria，或改用 `codex:manual-review` |
| 高風險偵測可能漏掉專案特定風險 | keyword 無法涵蓋所有 domain risk | 使用 `risk:high` label，必要時擴充高風險關鍵字 |
| 既有 worktree 有未提交變更 | 人工或其他 process 可能留下 dirty files | runner 會阻擋 reuse；需人工清理或保存後重跑 |
| push 或 PR 建立失敗 | 網路、權限、branch protection 或 GitHub 狀態可能中斷交付 | wrapper 會寫 `PM_Review.md`；修復後重跑或從 issue worktree 手動處理 |
| Codex shell 注入不可用 proxy | 根因在專案腳本外 | wrapper 會在 process 內清除 proxy；請透過 wrapper 執行，不要直接呼叫 raw Git/GitHub 命令 |
| 通知失敗 | LINE/Teams/webhook credentials 或網路可能不可用 | `PM_Review.md` 會記錄通知失敗，PM 可直接檢查 work item |
