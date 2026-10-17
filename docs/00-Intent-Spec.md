# Intent Spec: [功能或修補名稱]

Owner: PM
Last updated: YYYY-MM-DD

此文件用來在實作前收斂需求。若任一區塊缺漏導致無法安全實作，Agent 必須建立 `PM_Review.md` 並停止，不得自行創造需求。

## 1. Goal 目標

請用 2 到 5 句描述這次要達成的結果。

```text
[範例] 修正會員資料儲存時未驗證手機格式的問題，避免無效手機號碼進入後端資料庫。
```

## 2. Acceptance Criteria 驗收條件

請列出可驗證的條件。

- [ ] 條件 1：
- [ ] 條件 2：
- [ ] 條件 3：

## 3. Non-goals 不做什麼

請列出本次明確不處理的範圍，避免 Agent 擴大修改。

- 不做：
- 不做：

## 4. Constraints & Dependencies 限制與相依

請列出外部限制、版本限制、既有 API、資料格式、UI 規範或相依服務。

- 技術限制：
- 相依服務：
- 相容性要求：
- 時程或發布限制：

## 5. Change Contract Mapping 變更契約

請回答下列問題，協助判斷是否需要 Dual-Spec、API schema 或人工審查。

| 問題 | Y/N/N/A | 說明 |
|---|---|---|
| 是否變更 API endpoint、request 或 response？ |  |  |
| 是否變更資料庫 schema 或 migration？ |  |  |
| 是否變更 authentication / authorization / permission model？ |  |  |
| 是否涉及 secrets、金流、資安或 production 設定？ |  |  |
| 是否會刪除資料或執行 destructive operation？ |  |  |
| 是否影響多個 app、service 或 shared library？ |  |  |

若任一項為 Y，請補充對應 spec 或轉人工審查。

## 6. Validation Plan 驗證計畫

請列出預期驗證方式。若不確定，Agent 會依 `AGENTS.md` 的多語言驗證規則選擇最小必要驗證。

- Lint：
- Typecheck：
- Unit test：
- Integration test：
- Build：
- Manual check：

## 7. Risk Review 風險檢查

若涉及下列任一項，請加上 `risk:high` label，並進入 PM Review：

- authentication / authorization
- payment / billing
- security / secrets
- database migration
- production deployment
- infrastructure / Terraform / Kubernetes
- permission model
- data deletion
- destructive operation

## 8. Version Log 版本紀錄

- v0.1.0 - 初版 - YYYY-MM-DD - Owner

## Agent Gate Check

Agent 在開始實作前必須確認：

- [ ] Goal 是否明確。
- [ ] Acceptance Criteria 是否可驗證。
- [ ] Non-goals 是否足以限制 scope。
- [ ] 高風險項目是否已標示。
- [ ] 驗證方式是否可執行或可替代。
- [ ] 若需求不足，是否已建立 `PM_Review.md` 並停止。
