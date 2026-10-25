# 任務進度清單 (Task Checklist)

work-item-id: issue-single-current-semester-enforcement-v9
branch: feat/single-current-semester-enforcement-v9

## 當前學期 (IsCurrent) 唯一性防呆調整
- ✅ 業務邏輯檢視與防呆實作：
  - 檢視 `SystemAdmin/Index.cshtml.cs` 與 `Admin/Index.cshtml.cs` 之學期新增、儲存與編輯邏輯。
  - 當任何學期被勾選/設定為「目前學期 (`IsCurrent = true`)」時，自動將資料庫中其他所有學期的 `IsCurrent` 設為 `false`。
  - 防呆保護：若嘗試取消勾選唯一當前學期，系統會進行阻擋並強制維持，確保永遠至少且至多有 1 個當前學期。
  - 自動校正機制：於 `OnGetAsync` 中巡檢，若因手動資料異常出現多個或零個當前學期，自動修正保留最新一筆為 true。
  - 前後台全面對齊：`Admin/Index.cshtml.cs` 中讀取目前學期一律優先依 `s.IsCurrent` 查詢。
- ✅ 單元測試覆蓋：新增 `SemesterManagement_EnforcesSingleCurrentSemester_Correctly` 單元測試覆蓋新增、切換與防呆保護情境。
- ✅ 回歸測試與驗證：執行所有測試確保 100% 通過（24/24 通過）。
- ✅ 產出測試報告與交付報告。
