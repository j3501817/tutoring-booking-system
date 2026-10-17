# Intent Spec: Generic Template Tooling

**Owner: PM** | 最後更新：2026-05-08

## 1. Goal

將 template 調整為語言中立的自動化開發流程底座。Node/npm 只能代表 Agentic governance 工具層，不可讓使用者誤解為業務專案必須使用 Node.js。

## 2. Acceptance Criteria

- `package.json` scripts 使用 `agentic:*` 命名，避免 `npm test` 被誤解成應用程式測試。
- CI 區分 Agentic governance checks 與 application checks。
- 新增語言中立設定，預留 Python、Node.js、.NET C# 檢查命令。
- 預設不強迫任何應用程式語言測試。
- 驗證腳本仍能通過所有工作單與品質 gate。

## 3. Non-goals

- 不移除 Node governance scripts，因目前通知、驗證、secret scan 已以 Node 實作。
- 不導入 Python 或 .NET SDK dependency。
- 不替任何業務專案建立實際測試框架。

## 4. Constraints & Dependencies

- 目前 template 的治理腳本使用 Node.js。
- 套用到 Python / Node.js / .NET C# 專案時，應由 `agentic.config.json` 決定是否啟用應用程式檢查。

## 5. Change Contract Mapping

- 觸發器/路由/Function 變更：Y，CI workflow 與 npm scripts 語意調整。
- 通知策略變更：N。
- 資料來源或查詢邏輯變更：N。
- 輸出格式或檔名契約變更：Y，新增 `agentic.config.json`。
- 環境變數變更：N。

## 6. SDD Gate Check

- docs/00-Intent-Spec.md 相關段落已更新？N，本次先調整工具層，不改規格模板。
- 受影響 AC 已同步確認？Y。
- 版本記錄已更新？N/A，以工作單紀錄交付。
- 第二份規格文件需同步？N。
- 結論：允許進入 RD，PM 已直接指示調整。

## 7. Version Log

- v0.1.0 - 建立語言中立工具調整工作單 - 2026-05-08 - 作者：Agent
