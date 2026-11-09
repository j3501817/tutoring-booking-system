# Status — issue-fix-grade-filter-and-auto-progress-switch

- ✅ INTAKE & SPEC：確認家長週期預約「年別篩選」應顯示中文（`@g.Value`），且課程進度依今日日期是否落在起訖日期內自動切換為「目前開放進度」（反之則否）
- ✅ PLAN & DESIGN：規劃 `BookingService.SyncCurrentProgressesByDateAsync` 自動同步機制與修正 `#recurringGradeFilter` 選項文字
- ✅ BUILD：修改 `Parent/Index.cshtml(.cs)`、`Members/Index.cshtml(.cs)`、`Admin/Index.cshtml.cs`、`Teacher/Index.cshtml.cs`、`Pages/Index.cshtml.cs`、`BookingService.cs` 與單元測試
- ✅ REVIEW & VERIFY：執行 `dotnet test` 驗證通過（41/41 Passed）
- ✅ DELIVER：產出報告並直接發送新分支 PR
