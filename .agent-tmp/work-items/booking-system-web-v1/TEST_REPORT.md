# Test Report (`booking-system-web-v1`)

- **偵測到的技術棧**：
  - 應用程式執行環境：C# / ASP.NET Core (`.NET 10.0.401`) + Razor Pages + Entity Framework Core + xUnit
  - 專案治理工具鏈：Node.js (`npm run agentic:validate`, `npm run agentic:check-secrets`)
- **選擇驗證指令的理由**：
  - 依據 `AGENTS.md` 第 7 節與 `docs/00-Intent-Spec-Booking-System.md` 第 6 節驗證計畫，執行 `.NET` 解決方案編譯與 xUnit 單元／整合測試，並搭配儲存庫治理驗證腳本確保無機密外洩與工作單契約完整。
- **實際執行的指令與結果**：
  1. `/opt/homebrew/bin/dotnet test PlatoTutoring.sln`：**通過 (PASS)**
     - `PriorityDeduction_UsesBonusFirst_ThenPaid_AndReconcilesCleanly`：通過（驗證優先扣贈送堂數、再扣付費堂數，且夜間對帳 100% 吻合）
     - `Cancellation24HourBoundary_RefundsWhenAtLeast24Hours_NoRefundWhenUnder24Hours`：通過（驗證開課前 24 小時取消自動退堂，未滿 24 小時取消不退堂）
     - `WaitlistHold30Minutes_AutoRotatesWhenExpired_AndConfirmsWithinWindow`：通過（驗證名額釋出保留 30 分鐘、逾時自動輪替下一順位、時限內確認轉正式預約）
     - `ProRataSemesterBonus_SemesterExpiry_AndDropoutBatchSettlement_CalculateAccurately`：通過（驗證插班生學期贈送比例四捨五入、期末贈送歸零、退班依各儲值批次當時單價結算應退金額）
     - `Homework48HourReminder_AndGroupNoShowSuspension_WorkAsExpected`：通過（驗證作業不佳 48 小時二次催促標示、團體課 30 天內爽約 3 次自動停權 14 天）
  2. `npm run agentic:validate`：**通過 (PASS)**
  3. `npm run agentic:check-secrets`：**通過 (PASS)**
- **略過項目與原因**：無略過項目。
- **失敗摘要与是否已修復**：
  - 初次編譯時 `Pages/Parent/Index.cshtml` 第 77 行在 `@if` 區塊內多寫了一層 `@{`，已立即修正並重新執行 `dotnet test PlatoTutoring.sln` 全數通過。
- **是否需要 PM Review**：已完成 PM 核定規格之實作與測試，可供 PM 直接開啟瀏覽器驗收畫面。
