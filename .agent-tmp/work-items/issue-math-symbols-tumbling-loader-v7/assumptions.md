# 假定與決策紀錄 (Assumptions)

- **選擇方案**：PM 明確選定「方案 1：🌟 旗艦綜合款」。
- **技術選型**：採用純 CSS3 3D Transform (`perspective`, `transform-style: preserve-3d`, `rotateX/Y/Z`) 硬體加速，零第三方庫相依、極致輕量、不佔用額外資源。
- **色彩風格**：採用與系統相符的深藍科技色系 (`#3b82f6`, `#60a5fa`, `#93c5fd`, `#38bdf8`)，半透明遮罩背景 `rgba(15, 23, 42, 0.78)` 搭配 `backdrop-filter: blur(6px)`。
- **影響範圍**：僅限 `src/PlatoTutoring.Web/Pages/Shared/_Layout.cshtml` 之全域等待遮罩區塊，不影響後端邏輯或現有 API。
