# Work Item Status: issue-reconciliation-bonus-credit-sync

## 任務進度清單 (TODO Checklist)
- ✅ 建立工作單沙盒與目標分支 (`feat/reconciliation-bonus-credit-sync`)
- ✅ 診斷一鍵同步無效之根因：原同步函式僅比對付費堂數，未處理贈送堂數差額
- ✅ 擴充 `DbInitializer.EnsureCreditBatchesAndLedgerConsistentAsync` 同時補齊付費堂數、儲值批次與贈送堂數流水帳差額
- ✅ 新增單元測試 `EnsureCreditBatchesAndLedgerConsistent_FixesBothPaidAndBonusDiscrepancies` 並全數通過（32/32 Passed）
- ✅ 建立並交付資料庫直連可執行之 SQL 修復腳本
- ✅ Commit、Push 並建立 Pull Request
