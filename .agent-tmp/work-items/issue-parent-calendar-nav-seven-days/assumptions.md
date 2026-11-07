# Assumptions: issue-parent-calendar-nav-seven-days

## 需求分析
1. 家長預約行事曆維持一次顯示 7 天。
2. 使用者明確要求：
   - 「要可以往後七天跟往前七天。只是往前只到今天為止，往後暫時不限」
   - 「其餘功能皆不動」
3. 具體規格：
   - 基準日為今天（`today`，台北時間）。
   - 當前顯示區間由 `startDate` 決定，預設為 `today`，顯示天數為 7 天（`[startDate, startDate + 6 days]`）。
   - 「往前七天（上一週）」：
     - 若 `startDate <= today`，則處於最早可視週，按鈕停用（disabled）或不可點，確保絕不會往前顯示到今天以前的日期。
     - 若 `startDate > today`，往前 7 天的新起始日為 `Math.Max(today, startDate - 7 days)`。
   - 「往後七天（下一週）」：
     - 起始日移動為 `startDate + 7 days`，往後不設限。
   - 另可提供「返回本週 / 今天」按鈕（若當前不是本週），方便家長一鍵回航。
   - 標題可動態顯示當前週的日期範圍（例如：`2026/10/19 ～ 2026/10/25` 或當月份），清晰友善。
