# 任務進度清單 (Task Checklist)

work-item-id: issue-remove-noshow-penalty-v8
branch: feat/remove-noshow-penalty-v8

## 拔除爽約停權功能
- ✅ 業務邏輯盤點與拔除：全面搜尋專案中所有與「爽約停權（No-Show Penalty）」相關的邏輯。
  - 拔除預約流程中的停權阻擋檢查（`GroupBookingSuspendedUntilUtc`）。
  - 拔除點名確認（NoShow）時觸發的自動停權設定與停權記點。
  - 拔除系統管理與教務管理中的停權狀態判定與停權開關等介面與 Handler。
- ✅ 回歸測試與驗證：更新所有相依的單元測試，確保全數通過（23/23 通過）。
- ✅ 產出測試報告與交付報告。
