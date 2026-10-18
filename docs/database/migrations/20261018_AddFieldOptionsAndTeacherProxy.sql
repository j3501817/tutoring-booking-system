-- =============================================================================
-- 柏拉圖文理補習班 輔導預約系統 - Azure SQL / SQL Server 資料庫異動腳本
-- 檔案：docs/database/migrations/20261018_AddFieldOptionsAndTeacherProxy.sql
-- 用途：
--   1. 為 [BK_Students] 新增 [School]、[Grade]、[ClassName] 欄位並回填既有資料
--   2. 為 [BK_Users] 新增老師任教科目多選 [Subjects] 與代理管理權限 [IsProxyAdmin] 欄位
--   3. 建立系統管理 > 欄位管理資料表 [BK_FieldOptions]
--   4. 寫入預設下拉與多選選項：
--      - 學校 (School)：三民、培英、光華、竹光、育賢、私校
--      - 年別 (Grade)：國一、國二、國三
--      - 關係稱謂 (Relationship)：爸爸、媽媽
--      - 班級連動學校 (Class)：各校預設 A班、B班、C班
--      - 任教科目 (Subject)：國文、英文、數學、理化、社會
-- 特性：具備完整冪等性 (Idempotent)，可重複安全執行
-- =============================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

-- 1. 擴充 [BK_Students] 學校、年別、班級欄位
IF OBJECT_ID(N'[dbo].[BK_Students]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.BK_Students', 'School') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_Students]
        ADD [School] NVARCHAR(80) NOT NULL CONSTRAINT [DF_BK_Students_School] DEFAULT N'';
    END

    IF COL_LENGTH('dbo.BK_Students', 'Grade') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_Students]
        ADD [Grade] NVARCHAR(40) NOT NULL CONSTRAINT [DF_BK_Students_Grade] DEFAULT N'';
    END

    IF COL_LENGTH('dbo.BK_Students', 'ClassName') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_Students]
        ADD [ClassName] NVARCHAR(40) NOT NULL CONSTRAINT [DF_BK_Students_ClassName] DEFAULT N'';
    END
END
GO

-- 回填既有學生學校、年別、班級與家長關係稱謂
IF OBJECT_ID(N'[dbo].[BK_Students]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[BK_Students]
    SET
        [School] = CASE
            WHEN [School] = N'' OR [School] IN (N'敦化國中', N'介壽國中') THEN
                CASE WHEN [FullName] LIKE N'陳%' THEN N'培英' ELSE N'三民' END
            ELSE [School]
        END,
        [Grade] = CASE
            WHEN [Grade] = N'' AND LEN([GradeClass]) >= 2 THEN LEFT([GradeClass], 2)
            WHEN [Grade] = N'' THEN [GradeClass]
            ELSE [Grade]
        END,
        [ClassName] = CASE
            WHEN [ClassName] = N'' AND LEN([GradeClass]) > 2 THEN SUBSTRING([GradeClass], 3, 40)
            WHEN [ClassName] = N'' THEN N'A班'
            ELSE [ClassName]
        END;
END

IF OBJECT_ID(N'[dbo].[BK_ParentStudentLinks]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[BK_ParentStudentLinks]
    SET [Relationship] = CASE
        WHEN [Relationship] = N'母親' THEN N'媽媽'
        WHEN [Relationship] = N'父親' THEN N'爸爸'
        ELSE [Relationship]
    END
    WHERE [Relationship] IN (N'母親', N'父親');
END
GO

-- 2. 擴充 [BK_Users] 老師任教科目 (Subjects) 與代理權限 (IsProxyAdmin)
IF OBJECT_ID(N'[dbo].[BK_Users]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.BK_Users', 'Subjects') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_Users]
        ADD [Subjects] NVARCHAR(200) NOT NULL CONSTRAINT [DF_BK_Users_Subjects] DEFAULT N'';
    END

    IF COL_LENGTH('dbo.BK_Users', 'IsProxyAdmin') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BK_Users]
        ADD [IsProxyAdmin] BIT NOT NULL CONSTRAINT [DF_BK_Users_IsProxyAdmin] DEFAULT (0);
    END
END
GO

IF OBJECT_ID(N'[dbo].[BK_Users]', N'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[BK_Users]
    SET [Subjects] = N'數學,理化'
    WHERE [Role] = 2 AND [Subjects] = N'';
END
GO

-- 3. 建立系統管理 > 欄位管理資料表 [BK_FieldOptions]
IF OBJECT_ID(N'[dbo].[BK_FieldOptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_FieldOptions] (
        [Id]          INT            IDENTITY(1,1) NOT NULL,
        [Category]    NVARCHAR(40)   NOT NULL,
        [Value]       NVARCHAR(80)   NOT NULL,
        [ParentValue] NVARCHAR(80)   NULL,
        [SortOrder]   INT            NOT NULL CONSTRAINT [DF_BK_FieldOptions_SortOrder] DEFAULT (1),
        [IsActive]    BIT            NOT NULL CONSTRAINT [DF_BK_FieldOptions_IsActive] DEFAULT (1),
        CONSTRAINT [PK_BK_FieldOptions] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_BK_FieldOptions_Category]
        ON [dbo].[BK_FieldOptions] ([Category] ASC);
END
GO

-- 4. 寫入預設下拉選項（若尚未存在）
IF NOT EXISTS (SELECT 1 FROM [dbo].[BK_FieldOptions])
BEGIN
    -- 學校: [三民、培英、光華、竹光、育賢、私校]
    INSERT INTO [dbo].[BK_FieldOptions] ([Category], [Value], [ParentValue], [SortOrder], [IsActive]) VALUES
    (N'School', N'三民', NULL, 1, 1),
    (N'School', N'培英', NULL, 2, 1),
    (N'School', N'光華', NULL, 3, 1),
    (N'School', N'竹光', NULL, 4, 1),
    (N'School', N'育賢', NULL, 5, 1),
    (N'School', N'私校', NULL, 6, 1);

    -- 年別: [國一、國二、國三]
    INSERT INTO [dbo].[BK_FieldOptions] ([Category], [Value], [ParentValue], [SortOrder], [IsActive]) VALUES
    (N'Grade', N'國一', NULL, 1, 1),
    (N'Grade', N'國二', NULL, 2, 1),
    (N'Grade', N'國三', NULL, 3, 1);

    -- 關係稱謂: [爸爸、媽媽]
    INSERT INTO [dbo].[BK_FieldOptions] ([Category], [Value], [ParentValue], [SortOrder], [IsActive]) VALUES
    (N'Relationship', N'爸爸', NULL, 1, 1),
    (N'Relationship', N'媽媽', NULL, 2, 1);

    -- 班級（連動學校）: 各校預設 [A班、B班、C班]
    INSERT INTO [dbo].[BK_FieldOptions] ([Category], [Value], [ParentValue], [SortOrder], [IsActive]) VALUES
    (N'Class', N'A班', N'三民', 1, 1), (N'Class', N'B班', N'三民', 2, 1), (N'Class', N'C班', N'三民', 3, 1),
    (N'Class', N'A班', N'培英', 1, 1), (N'Class', N'B班', N'培英', 2, 1), (N'Class', N'C班', N'培英', 3, 1),
    (N'Class', N'A班', N'光華', 1, 1), (N'Class', N'B班', N'光華', 2, 1), (N'Class', N'C班', N'光華', 3, 1),
    (N'Class', N'A班', N'竹光', 1, 1), (N'Class', N'B班', N'竹光', 2, 1), (N'Class', N'C班', N'竹光', 3, 1),
    (N'Class', N'A班', N'育賢', 1, 1), (N'Class', N'B班', N'育賢', 2, 1), (N'Class', N'C班', N'育賢', 3, 1),
    (N'Class', N'A班', N'私校', 1, 1), (N'Class', N'B班', N'私校', 2, 1), (N'Class', N'C班', N'私校', 3, 1);

    -- 任教科目（老師多選）: [國文、英文、數學、理化、社會]
    INSERT INTO [dbo].[BK_FieldOptions] ([Category], [Value], [ParentValue], [SortOrder], [IsActive]) VALUES
    (N'Subject', N'國文', NULL, 1, 1),
    (N'Subject', N'英文', NULL, 2, 1),
    (N'Subject', N'數學', NULL, 3, 1),
    (N'Subject', N'理化', NULL, 4, 1),
    (N'Subject', N'社會', NULL, 5, 1);
END

COMMIT TRANSACTION;
GO
