# 任務進度清單 (Task Checklist)

work-item-id: issue-math-symbols-tumbling-loader-v7
branch: feat/math-symbols-tumbling-loader-v7

## 全域等待遮罩數理符號 3D 翻滾動畫 (方案 1 旗艦綜合款)
- ✅ 遮罩樣式改造：於 `_Layout.cshtml` 之全域等待遮罩 `#globalLoadingOverlay` 加入 3D 翻滾數理符號動畫效果。
  - 背景數理星空層：$\pi$、$\sum$、$\int$、$\sqrt{x}$、$\infty$、$\Delta$、$\theta$、$e^{i\pi}$ 緩慢 3D 翻滾漂浮與景深半透明呼吸感。
  - 中央 Spinner 核心：中心帶有發光數理符號 $\pi$，周邊搭配微光環繞軌道。
  - 標題後綴 3D 魔方：「處理中，請稍候...」字樣右側配置立體翻轉魔方，以 3D 透視旋轉展示各數理面。
- ✅ 跨裝置與響應式適配：支援各螢幕尺寸，手機版自動縮放，確保文字可讀性。
- ✅ 回歸驗證與測試：執行全數單元測試確保零副作用。
- ✅ 產出測試報告與交付報告。
