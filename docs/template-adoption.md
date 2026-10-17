# Template Adoption Flow

Taiwan time checked: 2026-05-28 16:21:02 +08:00

## 目的

採用 `agentic-project-template` 建立新專案時，必須避免把 template repository 的 Git history 帶進新專案。新專案應該有自己的第一個 commit，而不是繼承 template 的 commit 紀錄。

## 建議流程：GitHub Use This Template

1. 在 template repository 點選 `Use this template`。
2. 選擇 `Create a new repository`。
3. 設定新 repository 的 owner、名稱與 visibility。
4. Clone 新建立的 repository，不要 clone template repository。
5. 確認 remote 指向新 repository：

```powershell
git remote -v
```

6. 執行初始化：

```powershell
.\scripts\init-codex-template.ps1
```

## 替代流程：複製檔案但排除 `.git`

如果不使用 GitHub template 功能，請複製檔案但排除 `.git`、`node_modules` 與 `.agent-tmp`，再建立新的 Git repository：

```powershell
robocopy <template-path> <new-project-path> /E /XD .git node_modules .agent-tmp
Set-Location <new-project-path>
git init
git branch -M main
git remote add origin https://github.com/<owner>/<new-repo>.git
git add .
git commit -m "Initial commit"
git push -u origin main
.\scripts\init-codex-template.ps1
```

## 不建議流程

不要用以下方式建立新專案：

```powershell
git clone https://github.com/<template-owner>/<template-repo>.git <new-project>
Set-Location <new-project>
git remote set-url origin https://github.com/<owner>/<new-repo>.git
git push -u origin main
```

這會把 template 的 commit history 一起推到新 repository。

## 初始化 Guard

`scripts/init-codex-template.ps1` 會在建立 labels、profile cache 與 automation artifacts 前檢查：

- `origin` 是否仍指向 template repository。
- repository 是否為 shallow clone。
- repository 是否已經有多個既有 commits，可能代表 template history 被帶入。

若檢查失敗，初始化會停止，不會繼續設定 automation。

## 合法覆寫

如果是在既有產品 repository 內導入 template tooling，可明確允許既有 history：

```powershell
.\scripts\init-codex-template.ps1 -AllowExistingHistory
```

如果是在維護 `agentic-project-template` 本身，可明確允許 template repository 與既有 history：

```powershell
.\scripts\init-codex-template.ps1 -AllowTemplateRepository -AllowExistingHistory
```

## 人工確認

採用前請確認：

- `git remote -v` 的 `origin` 指向新專案 repository。
- 新專案初始狀態的 `git rev-list --count HEAD` 符合預期。
- `.git/shallow` 不存在。
- `.\scripts\init-codex-template.ps1` 不需要不明確的 override 才能通過。
