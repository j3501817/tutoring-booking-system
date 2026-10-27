# Assumptions & Design Decisions

## 1. 一鍵同步無效之根因分析
- **現象**：管理員在對帳彈窗點擊「一鍵自動同步校準」後，頁面重整但 22 筆異常依然存在。
- **根因**：原 `EnsureCreditBatchesAndLedgerConsistentAsync` 僅包含付費堂數（`PaidCredits`）與儲值批次之補齊邏輯，完全缺少贈送堂數（`BonusCredits`）之差額同步處理。截圖中 22 位學生付費堂數均已一致（8=8=8），異常均為「主檔贈送堂數 3 堂 != 流水帳贈送合計 0 堂」，導致原函式執行時直接跳過，未做任何更動。

## 2. 修正機制
- **同步贈送堂數**：計算 `s.BonusCredits - ledgerBonusSum`。若為正差額，補入 `LedgerTransactionType.SemesterBonusGrant`（+diff）；若為負差額，補入 `AdjustmentReversal`（-diff），將流水帳贈送合計校準至與主檔 100% 吻合。
- **同步付費堂數與批次**：維持並健全先進先出與批次補建邏輯，確保三方（主檔、批次、流水帳）付費與贈送皆完全一致。

## 3. SQL 交付策略
- 為提供最即時的修復途徑，除了程式碼層級的一鍵同步修正外，一併提供可直接於 SQL Server / Azure SQL / SQLite 執行的獨立修復腳本。
