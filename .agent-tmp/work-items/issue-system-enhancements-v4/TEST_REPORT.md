# 測試與驗證報告 (TEST REPORT)

work-item-id: issue-system-enhancements-v4
branch: feat/system-enhancements-v4

## 1. 偵測到的技術棧
- **Framework**: .NET 10 (ASP.NET Core Razor Pages, Entity Framework Core, IHostedService)
- **Database**: SQLite (local)
- **Frontend**: Bootstrap 5, Bootstrap Icons, Vanilla JavaScript

## 2. 選擇驗證指令與理由
- `dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore /p:EnableSourceControlManagerQueries=false`：對專案編譯與 Razor Page 語法進行靜態與符號檢查。

## 3. 執行指令與結果
- **指令**: `DOTNET_CLI_HOME=/tmp dotnet build src/PlatoTutoring.Web/PlatoTutoring.Web.csproj --no-restore /p:EnableSourceControlManagerQueries=false`
  - **結果**: 通過 (Pass)
  - **輸出摘要**: `PlatoTutoring.Web -> .../PlatoTutoring.Web.dll`，`0 個錯誤`，`0 個程式碼警告`。

## 4. 人工驗證項目
1. **班別不連動**：
   - 確認 `syncCascadingClasses`、`schoolClassMap`、`defaultClasses` 已完全自 `Members/Index.cshtml` 移除。
   - 新增與編輯學生之班別選單只抓取 `ClassOptions`（資優班、精英班），切換學校不再變動班別。
2. **全域等待遮罩**：
   - 在 `_Layout.cshtml` 引入全域 Overlay 與攔截監聽。表單提交時阻斷所有按鈕並提示「處理中，請稍候...」，防重複送出。
3. **學生/家長多維篩選**：
   - `Members/Index.cshtml` 列表上方配置學校、年別、班別篩選列與清除按鈕，後端對家庭群組進行過濾，分頁按鈕連動帶上篩選參數。
4. **系統管理子功能拆分**：
   - 系統管理拆分為「學期行事曆管理」與「系統欄位管理」兩大子分頁，不並排擠壓。
   - 側邊欄展開兩個獨立連結。
   - 月曆導覽區中洩漏的 HTML 標記已完全清理。
5. **贈送堂數每日 00:00 排程自動歸零**：
   - 註冊 `SemesterBonusExpirationBackgroundService`，每天台北時間 00:00 定時檢查逾期學期並自動歸零未用完之贈送堂數，流水帳記為 `SemesterBonusExpiry`。
