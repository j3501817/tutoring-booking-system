# Status — issue-mobile-parent-tabs-and-calendar

- ✅ INTAKE & SPEC：確認兩張附件截圖問題（中間 Tab 手機版折行視覺不佳、10/19 過期課堂資料誤顯與 7 日曆手機版寬度擠壓錯位）
- ✅ PLAN & DESIGN：規劃三等分單排 Tab 膠囊卡片、手機版 2x4 雙排自適應 7 日行事曆卡片（首日今天雙欄寬度 + 後 6 日單欄）、過濾今天以前的歷史時段並移除 fallback 至舊日期的邏輯
- ✅ BUILD：修改 `Parent/Index.cshtml`、`Parent/Index.cshtml.cs`、`wwwroot/css/site.css`、`Data/DbInitializer.cs` 與單元測試
- ✅ REVIEW & VERIFY：執行 `dotnet test` 驗證通過（38/38 Passed）
- ✅ DELIVER：產出報告並直接建立新分支 PR
