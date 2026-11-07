# Delivery Report: issue-parent-calendar-seven-days

## 需求概述
- 依據 PM 需求與附件截圖，家長預約月曆不再顯示整個月，改為只顯示今天以後一週共七天，今天之前的不再顯示，其餘功能（週期預約、標籤說明、時段點選展開、休假提示等）全數保留不動。

## 變更項目
1. **`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml.cs`**
   - 新增 `TodayDate` 與 `SevenDays` 屬性，精確計算台北時區當日起算未來 7 天（Today ~ Today+6）。
   - 更新預設選取日期邏輯：自動鎖定今天或未來一週內最近有課的一天，讓時段列表第一時間直觀呈現。
2. **`src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`**
   - 將月曆網格從整月 35 格（含前置補白與整月日期）改為單一列 7 個格子。
   - 星期列自動對應這 7 天的星期幾（週日～週六），並對今天特別標註。
   - 格子標題顯示 `M/d`（跨月亦清晰可見），今天具有「今天」標示。
   - 保留小組指導/團體輔導徽章、休假日標示、點擊展開當天課堂列表、週期預約彈窗等所有既有功能。
3. **`tests/PlatoTutoring.Tests/ParentCalendarBookingTests.cs`**
   - 新增 `SevenDays_GeneratesExactlySevenConsecutiveDaysFromTodayWithoutPastDates` 單元測試。
