# Architecture Review

時間：2026-05-08 11:57:28 +08:00

## 事實檢查範圍

- 已讀取：README.md、AGENTS.md、.github/copilot-instructions.md、.github/workflows/gate-check.yml
- 已讀取：docs/00-Intent-Spec.md、docs/DUAL-SPEC-UPGRADE.md、docs/schemas/*
- 已讀取：scripts/notify-pm.js、package.json、.env.example、.gitignore
- 已載入審查技能：code-review-and-quality、security-and-hardening
- 已執行驗證：npm.cmd audit --audit-level=high、npm.cmd run lint --if-present、npm.cmd run test --if-present、node scripts/notify-pm.js REQ-VERIFY-001

## 結論

目前 template 的文件層架構完整度不錯，已涵蓋沙盒隔離、8 段狀態機、PM Approve、Dual-Spec、斷路器與通知腳本。

主要缺口在「文件規範尚未充分轉成機器可驗證規則」。若未補強，後續自動化開發流程可能看似通過 gate，但實際上沒有測試、沒有有效 schema、或通知沒有指向正確工作單。

## Findings

### Blocker: notify-pm.js 讀錯 work item id

scripts/notify-pm.js 使用 `process.argv[1]` 作為工作單編號；Node.js 中 `process.argv[1]` 是腳本路徑，真正的第一個參數是 `process.argv[2]`。

影響：照文件執行 `node scripts/notify-pm.js <work-item-id>` 時，通知會把腳本路徑當成工作單 id，PM 可能被導向錯誤的 `.agent-tmp/work-items/.../PM_Review.md`。

建議：改為 `process.argv[2]`，並在缺少 id 時輸出錯誤與非 0 exit code。

### Risk: CI gate 只檢查任一 TEST_REPORT.md 存在

.github/workflows/gate-check.yml 目前只檢查 `.agent-tmp/work-items/*/TEST_REPORT.md` 是否存在。

影響：舊工作單或不相關工作單的 TEST_REPORT.md 可能讓新的 PR 通過 DELIVER Gate；目前也未檢查同一工作單的 Intent-Spec、status、PM Approve、SDD Gate 結論。

建議：新增 work-item manifest，例如 `.agent-tmp/work-items/<id>/work-item.json`，並讓 CI 驗證同一 id 底下的必要產物與狀態。

### Risk: lint/test gate 目前可能是 no-op

package.json 只有 dotenv dependency，沒有 scripts。CI 使用 `npm run lint --if-present` 與 `npm run test --if-present`，因此目前沒有 lint/test script 也會通過。

驗證結果：`npm.cmd run lint --if-present` 與 `npm.cmd run test --if-present` 皆通過但無輸出；直接執行 `npm.cmd test` 會回報 Missing script: "test"。

建議：template 至少補一個 `validate` script，負責檢查 work-item 結構、schema 非空、必要報告存在；應用專案可再擴充 lint/test/build。

### Risk: docs/schemas 內有空白契約檔

docs/schemas/internal-auth-api.json 與 docs/schemas/external-line-api.yaml 目前大小皆為 0 bytes。

影響：README 與 Intent-Spec 都把 schema 作為防幻覺與 API 對齊基礎，但空白檔無法提供可解析契約，容易造成「看起來有 schema、實際上沒有約束」。

建議：刪除空白 placeholder，或補成有效 JSON Schema/OpenAPI 範本，並加入 CI parse check。

### Risk: references 有空白查核表

.agent/references/performance-checklist.md 與 accessibility-checklist.md 目前是 0 bytes。

影響：技能與 README 已宣稱會依 references 執行品質檢查，但部分查核表沒有內容，Review Gate 的一致性不足。

建議：補上最小可用內容，或暫時不要宣稱這兩項為強制品質標準。

### Risk: .gitignore 對工作單報告採全量追蹤，缺少敏感資訊遮罩規則

.gitignore 強制保留 `.agent-tmp/work-items/**/*.md`。這符合可稽核性，但工作單內的 PM_Review、TEST_REPORT 可能包含錯誤日誌、路徑、環境變數片段或外部回應。

建議：新增 redaction checklist 或 pre-commit 檢查，至少掃描 secret/token/password/api_key 等高風險字串。

## 建議補充架構件

1. `work-item.json`：工作單 id、stage、owner、approve 狀態、風險等級、關聯 PR。
2. `PLAN.md` / `ASSIGNMENT.md` 範本：明確記錄 Architect 輸出與 Worker 檔案修改邊界。
3. `scripts/validate-work-item.js`：集中執行沙盒結構、必要報告、schema 非空、SDD Gate 結論檢查。
4. `scripts/check-secrets.js` 或 pre-commit hook：避免工作單報告與程式碼誤帶機密。
5. `docs/adr/` 與 ADR template：記錄流程治理與架構決策，避免 README/AGENTS/.github 指令漂移。
6. schema parse CI：JSON Schema/OpenAPI/YAML 至少要能 parse，不能是空檔。

## 驗證紀錄

- `npm.cmd audit --audit-level=high`：通過，0 vulnerabilities。
- `npm.cmd run lint --if-present`：通過，但目前無 lint script。
- `npm.cmd run test --if-present`：通過，但目前無 test script。
- `npm.cmd test`：失敗，Missing script: "test"。
- `node scripts/notify-pm.js REQ-VERIFY-001`：可執行；策略為 NONE 時不發送通知。
