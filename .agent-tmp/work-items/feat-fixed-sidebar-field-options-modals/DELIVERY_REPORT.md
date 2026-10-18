# 交付報告 (DELIVERY_REPORT.md)

## 變更摘要
1. **功能列與校徽**：左側功能列固定定位 (`position: fixed`) 不隨頁面捲動位移；上方 `P` 圖示替換為 `plato-logo.png` 並維持原有尺寸。
2. **系統管理 > 欄位管理 (`/SystemAdmin/Index`)**：支援新增、修改、刪除「學校」、「年別」、「班級（連動學校）」、「關係稱謂」、「任教科目」選項。
3. **學生與家長管理 (`/Members#tab-student-parent`)**：進入即顯示列表（10 筆/頁）、同一家長之多位學生合併於同一列顯示、新增與修改改為彈跳視窗、學校/年別/連動班級/關係稱謂皆改為下拉選單。
4. **老師管理 (`/Members#tab-teachers`)**：進入即顯示列表（10 筆/頁）、新增與修改改為彈跳視窗、任教科目改為多選、新增「勾選代理權限」開關。
5. **管理員與代理老師權限**：「管理員營運與報表」與「系統管理」僅限管理員及有勾選代理之老師使用。
6. **獨立 SQL 異動檔**：產出 `docs/database/migrations/20261018_AddFieldOptionsAndTeacherProxy.sql` 與 `docs/database/migrations/20261018_AddStudentSchoolGradeClass.sql`。
