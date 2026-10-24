# Status: feat/parent-calendar-slot-list-fix

- ✅ 問題定位：分析「點了日期沒有顯示列表」的原因（Razor HTML encode 引號導致 inline onclick 解析失敗）
- ✅ 修改月曆日期格子：移除「已約」徽章與「剩餘空位」提示，僅保留小組/團體標籤
- ✅ 確認單筆時段卡片內完整顯示「已預約」與「剩餘空位」
- ✅ 修復點擊日期展開列表的前端互動與邏輯（改用 this.dataset + 全域事件委派 + 鍵盤無障礙支援）
- ✅ 驗證與測試（`dotnet test` 17/17 通過、`dotnet build` 0 錯誤）
- ✅ 交付報告與沙盒文件整理
- 🚧 提交代碼並建立 Pull Request
