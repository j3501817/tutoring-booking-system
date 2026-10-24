# Assumptions: feat/parent-calendar-slot-list-fix

## 核心需求
1. **點了日期沒有顯示列表**：
   - 需確認點擊時段列表未顯示的原因：
     - 是前端 JavaScript 錯誤？（如全域函式找不到、被事件冒泡覆蓋、或者 CSS 的 `d-none` 未移除？）
     - 是後端 `AllCurrentSlots` 沒有資料？檢視 `OnGetAsync` 中的 `AllCurrentSlots` 與 `AvailableSlots`。
     - 是時區與日期字串格式不一致？例如前端 `slotDateStr` 與月曆點擊傳入的 `dateStr` 格式或時區對不起來？
2. **已預約狀態移至單筆卡片內**：
   - 月曆格子中移除綠色「已約」徽章。
   - 單筆卡片中原本就有已預約徽章與綠色邊框，確認並強化展示。
3. **剩餘空位移至單筆卡片內**：
   - 月曆格子中移除「餘 X 空位」或「已滿額」。
   - 單筆卡片中確認有「剩餘 X 空位」與進度條。
4. **範圍控制**：
   - 僅修改 `Parent/Index.cshtml`、`Parent/Index.cshtml.cs` 及相關 CSS/測試，不更動其他未相關功能。
