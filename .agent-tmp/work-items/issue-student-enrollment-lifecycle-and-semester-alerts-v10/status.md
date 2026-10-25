# 任務進度清單 (Task Checklist)

work-item-id: issue-student-enrollment-lifecycle-and-semester-alerts-v10
branch: feat/student-enrollment-lifecycle-and-semester-alerts-v10

## 1. 學期倒數 14 天黃色橫幅提醒
- ✅ 於後台管理員全域佈局頂部（`_Layout.cshtml`）加入提醒：當距離目前學期結束日小於等於 14 天時，管理員登入自動浮現醒目黃色提醒框：
  `⚠️ 目前學期將於 YYYY-MM-DD 結束（尚餘 X 天），請記得提前建立下一學期行事曆與排課進度！`，並附上一鍵直達學期行事曆捷徑按鈕。

## 2. 方案 A 學籍狀態與歷程管理 (本學期在學 / 未續報 / 續報 / 退班)
- ✅ 模型與實體設計：
  - 定義 `StudentEnrollmentStatus`：`Enrolled = 1` (本學期在學), `Unrenewed = 2` (未續報), `DroppedOut = 3` (退班)。
  - `Student` 實體新增 `EnrollmentStatus` 屬性。
  - 新增 `StudentStatusLog` 實體與 `BK_StudentStatusLogs` 資料表，紀錄學籍狀態異動時間軸歷程。
- ✅ 後台介面設計 (`Members/Index.cshtml` & `Index.cshtml.cs`)：
  - 學生列表呈現清晰自然語彙徽章（🟢 本學期在學、🟡 未續報、🔴 退班）。
  - 操作按鈕：在學生旁提供「標為未續報」按鈕；在未續報學生旁提供顯眼「👉 續報」按鈕。
  - 點擊「👉 續報」時，狀態即刻切換為「本學期在學」，並自動為該學生補發本學期贈送堂數（若當期尚未領取過），具防呆去重，並記錄於流水帳與學籍歷程。
  - 提供「歷程」按鈕與彈跳視窗 Modal，可隨時以時間軸檢視各學期狀態變更。
- ✅ 預約限制防呆：未續報與退班學生不可預約課堂（`CreateBookingAsync` 與 `JoinWaitlistAsync` 均嚴格阻擋並友善引導）。

## 3. 新學期生效時全體在籍學生自動補發
- ✅ 當新學期生效（新增設為當前或切換目前學期）時，系統自動批次掃描所有「本學期在學」之學生，自動補發新學期的預設贈堂（具備去重防呆），並寫入流水帳與狀態歷程。

## 4. 測試與交付
- ✅ 單元測試覆蓋狀態切換、自動補發、防呆去重、預約阻擋防呆（26/26 通過）。
- ✅ 產出測試報告 (`TEST_REPORT.md`) 與交付報告 (`DELIVERY_REPORT.md`)。
