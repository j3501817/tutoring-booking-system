# Plan

## Scope

- 明確定義安全採用 template 的使用流程。
- 更新 README 與 auto-fix 文件，避免新專案快速開始誤導使用者走 `clone -> set-url -> push`。
- 在 `scripts/init-codex-template.ps1` 加入 fail-fast guard，於初始化 labels 前擋下疑似保留 template Git history 的 repo。

## Tasks

- 補一份 `docs/template-adoption.md`，列出首選流程、替代流程、禁止流程與誤用處置。
- 更新 `README.md` 與 `docs/auto-fix-flow.md`，把採用流程放在初始化之前。
- 更新 `scripts/init-codex-template.ps1`，檢查 `origin` 是否仍指向 template repo、是否為 shallow clone、是否已有多筆 commit history。
- 執行 template governance 驗證，並將結果寫入 `TEST_REPORT.md`。
