# Delivery Report (`booking-system-web-v1`)

## 1. 交付摘要
依據 `docs/00-Intent-Spec-Booking-System.md` 與 PM 核定之技術與業務決策，完成「柏拉圖補習班－網頁輔導預約與堂數管理系統」全端網站（C# ASP.NET Core `.NET 10` + Razor Pages + EF Core + Bootstrap 5 Mobile-First RWD）與 xUnit 自動化測試套件開發。

## 2. 主要實作檔案清單
- **解決方案與專案檔**：
  - `PlatoTutoring.sln`
  - `src/PlatoTutoring.Web/PlatoTutoring.Web.csproj`
  - `tests/PlatoTutoring.Tests/PlatoTutoring.Tests.csproj`
- **領域模型與資料庫啟動**：
  - `src/PlatoTutoring.Web/Models/Entities.cs`（涵蓋三角色、學生、家長關聯、學期、進度、時段、預約、候補、儲值批次、不可篡改流水帳、匯款憑證、作業追蹤、LINE 通知紀錄、退班結算）
  - `src/PlatoTutoring.Web/Data/AppDbContext.cs`
  - `src/PlatoTutoring.Web/Data/DbInitializer.cs`（預載完整展示資料，含林媽媽 2 位孩子、王老師、蘇主任、W07 開放進度與時段）
- **核心商業邏輯服務**：
  - `src/PlatoTutoring.Web/Services/CreditLedgerService.cs`
  - `src/PlatoTutoring.Web/Services/BookingService.cs`
  - `src/PlatoTutoring.Web/Services/LineNotificationService.cs`
  - `src/PlatoTutoring.Web/Services/AttachmentStorageService.cs`
- **RWD 響應式網頁與端點**：
  - `src/PlatoTutoring.Web/Program.cs`
  - `src/PlatoTutoring.Web/wwwroot/css/site.css`
  - `src/PlatoTutoring.Web/Pages/Shared/_Layout.cshtml`（含頂部一鍵切換家長／老師／管理員測試身分列）
  - `src/PlatoTutoring.Web/Pages/Index.cshtml`
  - `src/PlatoTutoring.Web/Pages/Account/Login.cshtml`
  - `src/PlatoTutoring.Web/Pages/Parent/Index.cshtml`（家長預約、孩子切換、候補 30 分鐘確認、同進度改期、24h 取消退堂、流水帳查詢）
  - `src/PlatoTutoring.Web/Pages/Teacher/Index.cshtml`（老師儲值登錄、手機拍照／上傳匯款憑證預覽、贈送與沖正、代預約、每日點名與課後定案、作業不佳 LINE 催促）
  - `src/PlatoTutoring.Web/Pages/Admin/Index.cshtml`（營運月報表、學期與進度管理、期末贈送堂數歸零、插班生比例贈送、退班批次計價結算、夜間對帳、堂數 CSV 匯出）
- **自動化測試**：
  - `tests/PlatoTutoring.Tests/BookingAndLedgerTests.cs`
