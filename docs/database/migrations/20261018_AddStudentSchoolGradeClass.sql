-- =============================================================================
-- 資料庫異動腳本 (Migration SQL)
-- 日期：2026-10-18
-- 目的：為 [dbo].[BK_Students] 新增「學校 (School)」、「年級 (Grade)」、「班級 (ClassName)」三個欄位，並回填既有學生資料
-- 適用環境：Azure SQL Database / SQL Server（支援重複執行 Idempotent）
-- =============================================================================

-- 1. 新增 School (學校) 欄位
IF OBJECT_ID(N'dbo.BK_Students', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.BK_Students') AND name = N'School'
   )
BEGIN
    ALTER TABLE [dbo].[BK_Students]
        ADD [School] NVARCHAR(80) NOT NULL CONSTRAINT [DF_BK_Students_School] DEFAULT N'';
END;
GO

-- 2. 新增 Grade (年級) 欄位
IF OBJECT_ID(N'dbo.BK_Students', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.BK_Students') AND name = N'Grade'
   )
BEGIN
    ALTER TABLE [dbo].[BK_Students]
        ADD [Grade] NVARCHAR(40) NOT NULL CONSTRAINT [DF_BK_Students_Grade] DEFAULT N'';
END;
GO

-- 3. 新增 ClassName (班級) 欄位
IF OBJECT_ID(N'dbo.BK_Students', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.BK_Students') AND name = N'ClassName'
   )
BEGIN
    ALTER TABLE [dbo].[BK_Students]
        ADD [ClassName] NVARCHAR(40) NOT NULL CONSTRAINT [DF_BK_Students_ClassName] DEFAULT N'';
END;
GO

-- 4. 為既有學生資料回填預設的學校、年級與班級（若尚為空值）
IF OBJECT_ID(N'dbo.BK_Students', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[BK_Students]
    SET
        [School] = CASE
            WHEN [FullName] LIKE N'陳%' THEN N'介壽國中'
            ELSE N'敦化國中'
        END,
        [Grade] = CASE
            WHEN LEN([GradeClass]) >= 2 THEN SUBSTRING([GradeClass], 1, 2)
            ELSE [GradeClass]
        END,
        [ClassName] = CASE
            WHEN LEN([GradeClass]) > 2 THEN SUBSTRING([GradeClass], 3, LEN([GradeClass]) - 2)
            ELSE N'A班'
        END
    WHERE [School] = N'' AND [Grade] = N'';
END;
GO
