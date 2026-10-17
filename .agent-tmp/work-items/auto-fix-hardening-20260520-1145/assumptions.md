# Assumptions

- 使用者已確認一般 PowerShell 環境可執行 `gh --version`。
- Codex 執行環境可能與使用者 PowerShell PATH 不同，因此腳本仍需做 prerequisite 檢查並給出明確錯誤。
- GitHub Actions 僅作 CI 驗證，不在雲端執行 Codex。
