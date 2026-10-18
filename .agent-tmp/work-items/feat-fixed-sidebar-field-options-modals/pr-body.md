## 變更摘要
1. **左側功能列與校徽**：
   - 左方功能列改為 `position: fixed; top: 0; left: 0; bottom: 0; height: 100vh;` 固定不亂跑，右側主內容區自動留白對齊。
   - 上方的 `P` 圖示替換為柏拉圖校徽 (`plato-logo.png`)，並維持原有尺寸不變 (`38px × 38px`)。
2. **學生/家長/老師管理 － 學生與家長管理 (`/Members#tab-student-parent`)**：
   - 剛進入頁面時直接顯示列表，並以每頁 **10 筆** 分頁呈現。
   - 列表以家庭（家長）為單位合併顯示，**不同學生同個家長不再拆成兩筆**，一目瞭然呈現家長與每位學生的關係稱謂、學校、年別、班級與堂數。
   - 新增／修改學生與家長全面改為 **彈跳視窗 (Bootstrap Modal)**。
   - 學校、年別、班級、關係稱謂全部改為下拉式選單，其中 **班級連動所選學校**（預設 `[A班、B班、C班]`）。
3. **學生/家長/老師管理 － 老師管理 (`/Members#tab-teachers`)**：
   - 剛進入頁面時直接顯示列表，並以每頁 **10 筆** 分頁呈現。
   - 新增／修改老師改為 **彈跳視窗 (Bootstrap Modal)**。
   - **任教科目改為多選**（預設 `[國文、英文、數學、理化、社會]`），並支援 **勾選代理權限 (`IsProxyAdmin`)**。
4. **系統管理 > 欄位管理 (`/SystemAdmin/Index`) & 權限控管**：
   - 新增「系統管理 > 欄位管理」頁面，可針對 **學校、年別、班級（連動學校）、關係稱謂、任教科目** 進行新增、修改、刪除。
   - **管理員營運與報表** 與 **系統管理** 限制僅有 **管理員** 及 **有勾選代理的老師** 可看見與使用。
5. **獨立資料庫 SQL 異動檔**：
   - 產出 `docs/database/migrations/20261018_AddFieldOptionsAndTeacherProxy.sql` 與 `docs/database/migrations/20261018_AddStudentSchoolGradeClass.sql`，並同步更新 `docs/database/BK_Schema_AzureSql.sql`。

## 測試與驗證結果
- `dotnet test PlatoTutoring.sln --no-restore`：6 項測試全數通過（失敗: 0，通過: 6）。
