# Dual-Spec 使用指引

Dual-Spec 是指除了 Intent Spec 之外，再補上一份更精確的技術契約，例如 API schema、資料格式、事件格式或跨系統介面規格。

## 何時需要 Dual-Spec

下列情況建議使用 Dual-Spec：

- API endpoint、request、response 有變更。
- 資料庫 schema、migration、索引或資料修復有變更。
- 前後端、服務間、第三方系統間需要明確 contract。
- 需求含有複雜 business rule，光靠自然語言容易誤解。
- 變更會影響多個 repo、service、app 或 shared library。
- Reviewer 需要用機器可讀格式檢查契約。

## 可接受的 Dual-Spec 形式

- OpenAPI YAML：`docs/schemas/*.yaml`
- JSON Schema：`docs/schemas/*.json`
- Markdown contract spec：`docs/<feature>-contract.md`
- Event schema：`docs/schemas/<event>.json`
- DB migration plan：`docs/<feature>-migration-plan.md`

請優先使用專案既有格式。

## 導入步驟

### Step 1：完成 Intent Spec

先填寫：

```text
docs/00-Intent-Spec.md
```

確認目標、驗收條件、限制與不做範圍。

### Step 2：判斷是否需要 Dual-Spec

檢查 `Change Contract Mapping`。若 API、資料、權限、跨系統契約或高風險項目為 Y，請補上 Dual-Spec。

### Step 3：建立對應契約文件

範例：

```text
docs/schemas/internal-auth-api.json
docs/schemas/external-line-api.yaml
docs/member-profile-contract.md
```

### Step 4：在 Issue 或 Intent Spec 引用

GitHub Issue 應明確附上：

```markdown
## Spec references
- docs/00-Intent-Spec.md
- docs/schemas/internal-auth-api.json
```

### Step 5：驗證與 review

Agent 必須依 spec 實作，不得自行更改 contract。若實作需要變更 contract，必須在 PR 中明確說明，並由 PM 或 Reviewer 確認。

## 範例 A：API 變更

需要補：

- endpoint
- method
- request schema
- response schema
- error response
- backward compatibility
- migration note，若有

建議文件：

```text
docs/schemas/<feature>-api.yaml
```

## 範例 B：資料庫變更

需要補：

- schema 差異
- migration strategy
- rollback strategy
- data backfill
- downtime 或相容性風險
- 驗證方式

若涉及 production database，預設進入 PM Review。

## 範例 C：事件或第三方整合

需要補：

- event name
- producer
- consumer
- payload schema
- retry / idempotency
- timeout / failure handling
- security considerations

## 不需要 Dual-Spec 的情況

通常不需要：

- 單純 copy 調整。
- 小型 UI 樣式修正。
- 測試補強且不改 production behavior。
- 文件更新。

但若 Reviewer 或 PM 認為容易誤解，仍可要求補 spec。

## PR Review Gate

有 Dual-Spec 的 PR，Reviewer 應確認：

- PR diff 是否符合 spec。
- spec 是否與實作同步。
- 測試是否覆蓋 contract。
- 是否有 backward compatibility 風險。
- 是否需要更新文件或範例。

正式 merge 必須走 GitHub PR review，不得用 CLI 繞過審查。
