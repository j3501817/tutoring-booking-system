# 交付報告 (DELIVERY_REPORT)

## 任務摘要
- **Work Item ID**: `issue-single-current-semester-enforcement-v9`
- **Branch**: `feat/single-current-semester-enforcement-v9`
- **PM 指示**: 「當前學期 (IsCurrent)」的唯一性防呆調整。

## 變更項目說明
1. **當前學期唯一性強制防呆 (`SystemAdmin/Index.cshtml.cs`)**:
   - 建立共用防呆方法 `EnforceSingleCurrentSemesterAsync(int? targetCurrentSemesterId = null)`。
   - 當任何學期在新增、編輯或切換時被設為 `IsCurrent = true`，自動將其他所有學期的 `IsCurrent` 設為 `false`，確保全資料庫至多僅有 1 個進行中學期。
   - **唯一學期防取消保護**：若系統中僅有 1 個當前學期，且管理員試圖編輯並取消勾選，系統會自動阻擋並提示「系統必須保留至少一個目前進行中之學期，不可直接取消。若欲更換，請直接將目標學期設為目前學期。」強制維持其有效性。
   - **初始學期自動設定**：若系統建立第一個學期，自動強制設為進行中學期。
   - **自動巡檢校正**：於後台 `OnGetAsync` 進入時進行資料防呆校正，若資料異常（如手動改 SQL 造成多個 true 或零個 true），自動校正保留最新一筆為 true。
2. **跨頁面查詢統一化 (`Admin/Index.cshtml.cs`)**:
   - 修正查詢目前學期邏輯，全面對齊為 `_db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent)`，消除過去依 ID 或日期的查詢分歧。
3. **單元測試覆蓋 (`ComprehensiveEnhancementTests.cs`)**:
   - 新增 `SemesterManagement_EnforcesSingleCurrentSemester_Correctly`，涵蓋學期新增切換、排他性檢查、切換學期及取消防呆情境。
   - 24 項測試全數綠燈通過。

## 驗證結果
- 編譯建置成功（0 Error, 0 Warning）。
- 單元測試：24/24 通過（100%）。
