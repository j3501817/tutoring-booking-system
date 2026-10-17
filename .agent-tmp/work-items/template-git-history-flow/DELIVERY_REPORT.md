# Delivery Report

## Summary

- 新增 `docs/template-adoption.md`，明確定義不繼承 template Git history 的採用流程。
- 更新 `README.md`，補上從採用 template、初始化、設定驗證策略、建立 Issue、dry run、live automation、PR review 到日常維護的完整流程。
- 更新 `docs/auto-fix-flow.md`，把 safe template adoption 放在初始化之前。
- 更新 `scripts/init-codex-template.ps1`，在建立 GitHub labels 前檢查錯誤採用狀態。

## Guard Behavior

- `origin` 仍指向 template repository 時停止。
- shallow clone 時停止。
- 新專案已有多筆 commit history 且未明確 override 時停止。
- 既有產品 repo 可用 `-AllowExistingHistory`，維護 template 本身可用 `-AllowTemplateRepository -AllowExistingHistory`。

## Known Risks

- Guard 不能判斷所有自訂遷移情境；既有產品導入 template 時需要明確使用 override。
- Live 初始化未執行，避免實際改動 GitHub labels。

## PM Review

- 不需要。
