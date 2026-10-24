# 設計與假設 (assumptions.md)

## 問題分析
- 使用者點擊「週期預約」按鈕時無反應。
- 檢查發現按鈕使用 `onclick="openRecurringBookingModal()"`，但 `@section Scripts` 內漏掉了 `openRecurringBookingModal` 函式的定義。
- 當使用者點選時，瀏覽器在主執行緒噴出 `ReferenceError: openRecurringBookingModal is not defined`，導致按鈕完全無反應。

## 修復對策
1. 按鈕加上 `data-bs-toggle="modal" data-bs-target="#modalRecurringBooking"` 與 `onclick="openRecurringBookingModal()"` 雙重觸發。
2. 在 JavaScript 定義全域 `openRecurringBookingModal()` 函式，呼叫 `filterRecurringSlots()` 更新狀態並顯式開啟 Modal。
3. 監聽 `#modalRecurringBooking` 的 `show.bs.modal` 事件，保證每次點開彈窗都會自動套用星期與年別篩選。
