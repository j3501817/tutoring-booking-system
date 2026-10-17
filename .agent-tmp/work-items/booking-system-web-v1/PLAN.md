# Implementation Plan: 柏拉圖補習班－網頁輔導預約與堂數管理系統 (`booking-system-web-v1`)

## Overview
依據 `docs/00-Intent-Spec-Booking-System.md` 與 PM 核定決策，建置 C# ASP.NET Core (Razor Pages + REST API + EF Core + Bootstrap 5 RWD) 全端網站與單元／整合測試套件，完整涵蓋家長端（多位孩子切換、RWD 月曆／卡片時段預約、五種到班目的、同進度改期、24 小時取消退堂、候補登記與 30 分鐘確認、堂數與流水帳查詢）、老師端（時段建立、每日點名、作業不佳標記與 LINE 催促、代學生預約、儲值登錄與匯款憑證上傳、贈送與沖正堂數）及管理員端（學期與進度管理、學期贈送與歸零、退班批次計價結算、CSV 匯出、對帳與營運月報表）。

## Architecture Decisions
1. **專案結構 (`src/PlatoTutoring.Web` & `tests/PlatoTutoring.Tests`)**：
   - 單一 Web App 承載 Razor Pages UI 與 `/api/*` 端點，符合 Azure App Service (Linux Code Deployment) 規範。
   - 資料層使用 Entity Framework Core (`AppDbContext`)，支援 SQLite（本機開發／自動化測試）與 Azure SQL Server（正式環境）。
2. **交易與併發控制**：
   - 預約、候補確認、取消退堂、儲值與堂數調整皆封裝於 `BookingService` 與 `CreditLedgerService`，確保名額不超賣、不重複扣堂、流水帳與主檔餘額恆等。
3. **RWD Mobile-First 介面設計**：
   - 針對 `360px`、`390px`、`768px`、`1280px` 斷點設計：電腦版採用雙欄／月曆與清單並排；手機版自動切換為直式日期篩選與大觸控區時段卡片、單欄表單與卡片式報表。

## Task List

### Phase 1: 環境準備與專案骨架
- Task 1: 安裝本機 .NET SDK 並建立 Solution (`PlatoTutoring.sln`)、Web 專案 (`src/PlatoTutoring.Web`) 與測試專案 (`tests/PlatoTutoring.Tests`)。
- Task 2: 建立 EF Core 領域模型 (`Models/Entities.cs`) 與 `AppDbContext`，涵蓋使用者、家長孩子關聯、學期、進度、輔導時段、預約、候補、儲值批次、不可篡改堂數流水帳、匯款附件、點名與作業追蹤、LINE 通知紀錄與退班結算。

### Phase 2: 核心商業邏輯與服務層
- Task 3: 實作 `CreditLedgerService`（優先扣贈送堂數、付費堂數批次追蹤、沖正調整、學期比例贈送與期末歸零、退班按批次單價結算、每日對帳檢查）。
- Task 4: 實作 `BookingService`（小組每進度限 1 堂、團體免扣堂、24 小時取消退堂邊界、同進度改期、候補登記、名額釋出保留 30 分鐘通知與逾時自動輪替、團體課 30 天爽約 3 次停權 14 天檢查）。
- Task 5: 實作 `LineNotificationService`（作業不佳催促、48 小時二次提醒、候補釋出通知、冪等紀錄與用量統計）與 `AttachmentStorageService`（私有憑證上傳驗證、90 天到期清除政策）。

### Phase 3: 身分驗證與三角色 RWD 網頁介面
- Task 6: 實作共用 RWD Layout、導覽列、登入／角色快速切換頁 (`/Account/Login`) 與種子測試資料 (`DbInitializer`)。
- Task 7: 實作**家長端頁面 (`/Parent`)**：孩子切換、剩餘堂數（付費／贈送分軌）與流水帳明細、RWD 預約月曆／手機直式卡片、五種到班目的預約確認彈窗、我的預約（同進度改期／取消）、候補 30 分鐘確認專區。
- Task 8: 實作**老師端頁面 (`/Teacher`)**：常態／加開時段建立、今日點名（出席／缺席／爽約）與作業不佳勾選催促、代學生預約、學生儲值登錄（含末五碼與手機拍照／相簿上傳匯款憑證預覽）、贈送堂數與沖正調整。
- Task 9: 實作**管理員端頁面 (`/Admin`)**：學生／家長／班別／學期進度與五種到班目的設定、退班結算計算器、堂數 CSV 匯出、夜間對帳與 90 天附件清理觸發、月度營運報表（儲值入帳、預收款、出席率、滿座率、到班目的分佈、作業追蹤、LINE 推播用量）。

### Phase 4: 自動化測試與治理驗證
- Task 10: 撰寫並執行 xUnit 單元與整合測試（驗證優先扣贈送堂數、24 小時退堂邊界、30 分鐘候補輪替、插班生比例贈送、退班批次計價、爽約記點停權、對帳一致性），並執行治理驗證腳本與產出 `TEST_REPORT.md`、`DELIVERY_REPORT.md`。
