# 任務狀態追蹤 (feat-fixed-sidebar-field-options-modals)

- ✅ 固定左側功能列 (`position: fixed`) 與上方 `P` 圖示替換為柏拉圖校徽（維持原尺寸）
- ✅ 系統管理 > 欄位管理 (`/SystemAdmin/Index`)：集中管理學校、年別、班級（連動學校）、關係稱謂、任教科目
- ✅ 學生與家長管理 (`/Members#tab-student-parent`)：進入即顯示列表（10筆/頁）、同家長學生合併同一列、彈跳視窗新增修改、下拉連動班級
- ✅ 老師管理 (`/Members#tab-teachers`)：進入即顯示列表（10筆/頁）、彈跳視窗新增修改、任教科目多選、代理權限勾選 (`IsProxyAdmin`)
- ✅ 權限控管：管理員營運與報表 (`/Admin`) 及系統管理 (`/SystemAdmin`) 僅限管理員與已勾選代理之老師使用
- ✅ 產出獨立 SQL 資料庫異動腳本 (`docs/database/migrations/20261018_AddFieldOptionsAndTeacherProxy.sql`)
- ✅ 執行 `dotnet test PlatoTutoring.sln --no-restore` 全數通過並建立 Pull Request
