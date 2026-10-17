# Assumptions

- 台灣時間已於 2026-05-27 17:21:01 +08:00 確認。
- 需求目標是讓團隊採用此 template 建立新系統時，不會因為 `git clone` 後改 remote 而保留 template 原始作者的 Git history。
- 允許修改文件與初始化腳本；不涉及 production config、secrets、DB migration 或 destructive operation。
- GitHub 官方文件指出，從 template 建立 repo 會以單一 commit 開始；fork 則會包含 parent repo 的完整 commit history。
