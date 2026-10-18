# 假設與設計決策紀錄 (assumptions.md)

1. **獨立 SQL 異動檔產出**：除透過 `DbInitializer` 於應用程式啟動時自動補齊資料表與欄位外，額外產出獨立可重入 (Idempotent) 的 Azure SQL 異動腳本 `docs/database/migrations/20261018_AddFieldOptionsAndTeacherProxy.sql` 與 `docs/database/migrations/20261018_AddStudentSchoolGradeClass.sql`，方便直接於資料庫執行。
2. **家長與學生合併列表**：於「學生與家長管理」中，以家庭（家長）為單位合併顯示同一位家長名下的所有孩子，不同學生同個家長不再拆成多筆，並以每頁 10 筆分頁呈現。
3. **代理管理權限 (`IsProxyAdmin`)**：於老師新增／修改彈跳視窗提供「勾選代理權限」開關，勾選後即可存取「管理員營運與報表」及「系統管理 > 欄位管理」。
