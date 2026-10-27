# Delivery Report: issue-reconciliation-bonus-credit-sync

## 1. 任務與變更摘要
- 修復三方對帳「一鍵自動同步校準」未包含「贈送堂數」流水帳補齊邏輯之問題。
- 擴充 `EnsureCreditBatchesAndLedgerConsistentAsync`，讓系統在執行校準時：
  1. 自動補建/調整付費儲值批次（`TopUpBatches`）。
  2. 自動補建/沖正付費堂數流水帳（`CreditLedgerEntries PaidDelta`）。
  3. 自動補建/沖正贈送堂數流水帳（`CreditLedgerEntries BonusDelta`）。
- 執行後使所有學生之主檔餘額、流水帳合計與儲值批次 100% 吻合，異常筆數歸零。
- 提供可直接於資料庫執行的 SQL 腳本。

## 2. 修改檔案清單
- `src/PlatoTutoring.Web/Data/DbInitializer.cs`
- `tests/PlatoTutoring.Tests/BookingAndLedgerTests.cs`
- `.agent-tmp/work-items/issue-reconciliation-bonus-credit-sync/*`
