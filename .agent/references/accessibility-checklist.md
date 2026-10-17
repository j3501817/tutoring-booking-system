# ♿ Accessibility Checklist

本清單供 `frontend-ui-engineering`、`REVIEW Gate` 與 `VERIFY Gate` 使用。若任務涉及 UI，Reviewer 必須至少檢查下列項目。

## 1. 語意與鍵盤操作

- [ ] 互動元素使用正確語意元素，例如 button、input、select、a。
- [ ] 所有可互動元素可用鍵盤操作，焦點順序符合畫面閱讀順序。
- [ ] Modal、選單、彈出層有明確焦點管理與關閉方式。

## 2. 標籤與狀態

- [ ] 表單欄位有可辨識 label 或 aria-label。
- [ ] 圖示按鈕有可理解的 accessible name。
- [ ] 錯誤、載入、成功狀態不只依賴顏色傳達。

## 3. 視覺可讀性

- [ ] 文字與背景對比符合 WCAG AA 常見門檻。
- [ ] 文字在手機與桌面寬度下不重疊、不截斷關鍵資訊。
- [ ] 動畫或閃爍效果不影響閱讀，必要時尊重 prefers-reduced-motion。

## 4. 驗證紀錄

- [ ] UI 變更需在 `TEST_REPORT.md` 記錄鍵盤操作、主要狀態與螢幕尺寸檢查結果。
