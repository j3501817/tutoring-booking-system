# Assumptions: issue-calendar-slot-type-badges

1. **輔導類型名詞對齊**：
   - 需求提及「顯示團體指導跟團體輔導」，對應系統現有開課類型為「小組指導」（`SlotType.SmallGroup`，藍色徽章 `badge-slot-small`）與「團體輔導」（`SlotType.GroupCounseling`，綠色徽章 `badge-slot-group`）。
   - 標籤文字與月曆右上角圖例 `[小組指導]`、`[團體輔導]` 100% 一致。

2. **單一徽章與不帶數字**：
   - 當天若有多堂同類型課程（例如 2 堂或多堂），月曆格子僅顯示該類型 1 個徽章。
   - 移除原先的數量數字（例如 `小組 1`、`團體 1`），改為純文字 `小組指導` 與 `團體輔導`，讓家長一眼即可識別當天開設之輔導類型。

3. **資料庫與 SQL 執行**：
   - 本次修改純為前端 Razor View 顯示樣式調整，無須執行任何資料庫 Migration 或 SQL 腳本。
