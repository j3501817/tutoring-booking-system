-- =============================================================================
-- 柏拉圖補習班－網頁輔導預約與堂數管理系統 (Azure SQL / SQL Server Schema)
-- 所有資料表統一以 BK_ 開頭，支援可重入執行 (Idempotent IF OBJECT_ID IS NULL)
-- =============================================================================

-- 1. BK_Users (家長、老師、管理員帳號表)
IF OBJECT_ID(N'dbo.BK_Users', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_Users] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(80) NOT NULL,
        [PasswordHash] NVARCHAR(120) NOT NULL,
        [DisplayName] NVARCHAR(80) NOT NULL,
        [Role] INT NOT NULL, -- 0: Parent, 1: Teacher, 2: Admin
        [Phone] NVARCHAR(40) NOT NULL,
        [LineUserId] NVARCHAR(100) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_Users] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_BK_Users_Username] ON [dbo].[BK_Users] ([Username]);
END;
GO

-- 2. BK_Students (學生與分軌堂數主檔)
IF OBJECT_ID(N'dbo.BK_Students', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_Students] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [FullName] NVARCHAR(80) NOT NULL,
        [GradeClass] NVARCHAR(50) NOT NULL,
        [EnrollmentDate] DATE NOT NULL,
        [PaidCredits] INT NOT NULL DEFAULT 0,
        [BonusCredits] INT NOT NULL DEFAULT 0,
        [IsDroppedOut] BIT NOT NULL DEFAULT 0,
        [DroppedOutAtUtc] DATETIME2 NULL,
        [GroupBookingSuspendedUntilUtc] DATETIME2 NULL,
        CONSTRAINT [PK_BK_Students] PRIMARY KEY ([Id])
    );
END;
GO

-- 3. BK_Semesters (學期設定與預設贈送堂數表)
IF OBJECT_ID(N'dbo.BK_Semesters', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_Semesters] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(80) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [DefaultBonusCredits] INT NOT NULL DEFAULT 3,
        [IsCurrent] BIT NOT NULL DEFAULT 0,
        [IsEnded] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_BK_Semesters] PRIMARY KEY ([Id])
    );
END;
GO

-- 4. BK_AttendancePurposeOptions (到班目的選項表)
IF OBJECT_ID(N'dbo.BK_AttendancePurposeOptions', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_AttendancePurposeOptions] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(60) NOT NULL,
        [SortOrder] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [PK_BK_AttendancePurposeOptions] PRIMARY KEY ([Id])
    );
END;
GO

-- 5. BK_SystemSettings (系統規則參數表)
IF OBJECT_ID(N'dbo.BK_SystemSettings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_SystemSettings] (
        [Key] NVARCHAR(80) NOT NULL,
        [Value] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(200) NOT NULL,
        CONSTRAINT [PK_BK_SystemSettings] PRIMARY KEY ([Key])
    );
END;
GO

-- 6. BK_RemittanceAttachments (匯款憑證附件 Metadata 表，實體檔案存於 Azure Blob Storage)
IF OBJECT_ID(N'dbo.BK_RemittanceAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_RemittanceAttachments] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [OriginalFileName] NVARCHAR(200) NOT NULL,
        [StoredFileName] NVARCHAR(200) NOT NULL,
        [ContentType] NVARCHAR(100) NOT NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [UploadedByUserId] INT NOT NULL,
        [UploadedAtUtc] DATETIME2 NOT NULL,
        [ExpiresAtUtc] DATETIME2 NOT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_BK_RemittanceAttachments] PRIMARY KEY ([Id])
    );
END;
GO

-- 7. BK_ParentStudentLinks (家長帳號與學生多對多授權關聯表)
IF OBJECT_ID(N'dbo.BK_ParentStudentLinks', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_ParentStudentLinks] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ParentUserId] INT NOT NULL,
        [StudentId] INT NOT NULL,
        [Relationship] NVARCHAR(40) NOT NULL,
        CONSTRAINT [PK_BK_ParentStudentLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_ParentStudentLinks_BK_Users_ParentUserId] FOREIGN KEY ([ParentUserId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_ParentStudentLinks_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_BK_ParentStudentLinks_ParentUserId_StudentId] ON [dbo].[BK_ParentStudentLinks] ([ParentUserId], [StudentId]);
    CREATE INDEX [IX_BK_ParentStudentLinks_StudentId] ON [dbo].[BK_ParentStudentLinks] ([StudentId]);
END;
GO

-- 8. BK_CourseProgresses (每週課程進度表)
IF OBJECT_ID(N'dbo.BK_CourseProgresses', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_CourseProgresses] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SemesterId] INT NOT NULL,
        [Code] NVARCHAR(40) NOT NULL,
        [Title] NVARCHAR(120) NOT NULL,
        [GradeClass] NVARCHAR(50) NOT NULL,
        [WeekStartDate] DATE NOT NULL,
        [WeekEndDate] DATE NOT NULL,
        [IsCurrentProgress] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_BK_CourseProgresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_CourseProgresses_BK_Semesters_SemesterId] FOREIGN KEY ([SemesterId]) REFERENCES [dbo].[BK_Semesters] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_CourseProgresses_SemesterId] ON [dbo].[BK_CourseProgresses] ([SemesterId]);
END;
GO

-- 9. BK_TutoringSlots (輔導時段表：小組指導 / 團體輔導)
IF OBJECT_ID(N'dbo.BK_TutoringSlots', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_TutoringSlots] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [TeacherUserId] INT NOT NULL,
        [CourseProgressId] INT NOT NULL,
        [GradeClass] NVARCHAR(50) NOT NULL,
        [SlotType] INT NOT NULL, -- 0: SmallGroup (扣1堂), 1: GroupCounseling (免費0堂)
        [StartsAtUtc] DATETIME2 NOT NULL,
        [EndsAtUtc] DATETIME2 NOT NULL,
        [MaxCapacity] INT NOT NULL,
        [CurrentBookedCount] INT NOT NULL DEFAULT 0,
        [IsExtraSlot] BIT NOT NULL DEFAULT 0,
        [Notes] NVARCHAR(200) NOT NULL,
        [IsAttendanceFinalized] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_BK_TutoringSlots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_TutoringSlots_BK_Users_TeacherUserId] FOREIGN KEY ([TeacherUserId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_TutoringSlots_BK_CourseProgresses_CourseProgressId] FOREIGN KEY ([CourseProgressId]) REFERENCES [dbo].[BK_CourseProgresses] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_TutoringSlots_TeacherUserId] ON [dbo].[BK_TutoringSlots] ([TeacherUserId]);
    CREATE INDEX [IX_BK_TutoringSlots_CourseProgressId] ON [dbo].[BK_TutoringSlots] ([CourseProgressId]);
END;
GO

-- 10. BK_Bookings (學生預約與課堂點名紀錄表)
IF OBJECT_ID(N'dbo.BK_Bookings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_Bookings] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [TutoringSlotId] INT NOT NULL,
        [StudentId] INT NOT NULL,
        [CreatedByUserId] INT NOT NULL,
        [CreatedByName] NVARCHAR(80) NOT NULL,
        [CreatedByRole] INT NOT NULL,
        [PurposeName] NVARCHAR(60) NOT NULL,
        [Status] INT NOT NULL, -- 0: Confirmed, 1: CancelledOnTime, 2: CancelledLate, 3: Rescheduled
        [CreditsDeducted] INT NOT NULL,
        [BonusCreditsUsed] INT NOT NULL,
        [PaidCreditsUsed] INT NOT NULL,
        [AttendanceStatus] INT NOT NULL, -- 0: Pending, 1: Present, 2: Absent, 3: NoShow
        [CreatedAtUtc] DATETIME2 NOT NULL,
        [CancelledAtUtc] DATETIME2 NULL,
        [CancelReason] NVARCHAR(200) NULL,
        CONSTRAINT [PK_BK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_Bookings_BK_TutoringSlots_TutoringSlotId] FOREIGN KEY ([TutoringSlotId]) REFERENCES [dbo].[BK_TutoringSlots] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_Bookings_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_Bookings_TutoringSlotId] ON [dbo].[BK_Bookings] ([TutoringSlotId]);
    CREATE INDEX [IX_BK_Bookings_StudentId] ON [dbo].[BK_Bookings] ([StudentId]);
END;
GO

-- 11. BK_WaitlistEntries (滿額候補與 30 分鐘保留佇列表)
IF OBJECT_ID(N'dbo.BK_WaitlistEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_WaitlistEntries] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [TutoringSlotId] INT NOT NULL,
        [StudentId] INT NOT NULL,
        [ParentUserId] INT NOT NULL,
        [PurposeName] NVARCHAR(60) NOT NULL,
        [Position] INT NOT NULL,
        [Status] INT NOT NULL, -- 0: Waiting, 1: NotifiedHold, 2: ConfirmedToBooking, 3: Expired, 4: Cancelled
        [NotifiedAtUtc] DATETIME2 NULL,
        [HoldExpiresAtUtc] DATETIME2 NULL,
        [ConfirmedAtUtc] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_WaitlistEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_WaitlistEntries_BK_TutoringSlots_TutoringSlotId] FOREIGN KEY ([TutoringSlotId]) REFERENCES [dbo].[BK_TutoringSlots] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_WaitlistEntries_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_WaitlistEntries_BK_Users_ParentUserId] FOREIGN KEY ([ParentUserId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_WaitlistEntries_TutoringSlotId] ON [dbo].[BK_WaitlistEntries] ([TutoringSlotId]);
    CREATE INDEX [IX_BK_WaitlistEntries_StudentId] ON [dbo].[BK_WaitlistEntries] ([StudentId]);
    CREATE INDEX [IX_BK_WaitlistEntries_ParentUserId] ON [dbo].[BK_WaitlistEntries] ([ParentUserId]);
END;
GO

-- 12. BK_TopUpBatches (匯款儲值批次表，支援退班按各批單價精算)
IF OBJECT_ID(N'dbo.BK_TopUpBatches', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_TopUpBatches] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [StudentId] INT NOT NULL,
        [RecordedByTeacherId] INT NOT NULL,
        [AmountNtd] DECIMAL(12,2) NOT NULL,
        [CreditsAdded] INT NOT NULL,
        [UnitPriceNtd] DECIMAL(12,2) NOT NULL,
        [RemainingPaidCredits] INT NOT NULL,
        [RemittanceDate] DATE NOT NULL,
        [AccountLastFiveDigits] NVARCHAR(10) NOT NULL,
        [Note] NVARCHAR(200) NOT NULL,
        [AttachmentId] INT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_TopUpBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_TopUpBatches_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_TopUpBatches_BK_Users_RecordedByTeacherId] FOREIGN KEY ([RecordedByTeacherId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_TopUpBatches_BK_RemittanceAttachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [dbo].[BK_RemittanceAttachments] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_TopUpBatches_StudentId] ON [dbo].[BK_TopUpBatches] ([StudentId]);
    CREATE INDEX [IX_BK_TopUpBatches_RecordedByTeacherId] ON [dbo].[BK_TopUpBatches] ([RecordedByTeacherId]);
    CREATE INDEX [IX_BK_TopUpBatches_AttachmentId] ON [dbo].[BK_TopUpBatches] ([AttachmentId]);
END;
GO

-- 13. BK_CreditLedgerEntries (不可篡改之堂數異動流水帳表)
IF OBJECT_ID(N'dbo.BK_CreditLedgerEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_CreditLedgerEntries] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [StudentId] INT NOT NULL,
        [TransactionType] INT NOT NULL,
        [PaidDelta] INT NOT NULL,
        [BonusDelta] INT NOT NULL,
        [PaidBalanceAfter] INT NOT NULL,
        [BonusBalanceAfter] INT NOT NULL,
        [RelatedBookingId] INT NULL,
        [RelatedTopUpBatchId] INT NULL,
        [ReferenceNote] NVARCHAR(250) NOT NULL,
        [OperatorUserId] INT NOT NULL,
        [OperatorName] NVARCHAR(80) NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_CreditLedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_CreditLedgerEntries_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_CreditLedgerEntries_StudentId] ON [dbo].[BK_CreditLedgerEntries] ([StudentId]);
END;
GO

-- 14. BK_DropoutSettlements (退班結算歷史稽核表)
IF OBJECT_ID(N'dbo.BK_DropoutSettlements', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_DropoutSettlements] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [StudentId] INT NOT NULL,
        [ProcessedByAdminId] INT NOT NULL,
        [ProcessedByAdminName] NVARCHAR(MAX) NOT NULL,
        [TotalRefundablePaidCredits] INT NOT NULL,
        [CalculatedRefundAmountNtd] DECIMAL(12,2) NOT NULL,
        [BreakdownSummary] NVARCHAR(MAX) NOT NULL,
        [Note] NVARCHAR(250) NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_DropoutSettlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_DropoutSettlements_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_DropoutSettlements_StudentId] ON [dbo].[BK_DropoutSettlements] ([StudentId]);
END;
GO

-- 15. BK_HomeworkWatchItems (作業不佳追蹤與 48 小時二次催促表)
IF OBJECT_ID(N'dbo.BK_HomeworkWatchItems', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_HomeworkWatchItems] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [StudentId] INT NOT NULL,
        [TeacherUserId] INT NOT NULL,
        [CourseProgressId] INT NOT NULL,
        [SubjectNote] NVARCHAR(200) NOT NULL,
        [FirstNotifiedAtUtc] DATETIME2 NOT NULL,
        [SecondReminderSentAtUtc] DATETIME2 NULL,
        [IsSecondReminderFlagged] BIT NOT NULL DEFAULT 0,
        [IsResolvedByBooking] BIT NOT NULL DEFAULT 0,
        [ResolvedBookingId] INT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_HomeworkWatchItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_HomeworkWatchItems_BK_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[BK_Students] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_HomeworkWatchItems_BK_Users_TeacherUserId] FOREIGN KEY ([TeacherUserId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BK_HomeworkWatchItems_BK_CourseProgresses_CourseProgressId] FOREIGN KEY ([CourseProgressId]) REFERENCES [dbo].[BK_CourseProgresses] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_BK_HomeworkWatchItems_StudentId] ON [dbo].[BK_HomeworkWatchItems] ([StudentId]);
    CREATE INDEX [IX_BK_HomeworkWatchItems_TeacherUserId] ON [dbo].[BK_HomeworkWatchItems] ([TeacherUserId]);
    CREATE INDEX [IX_BK_HomeworkWatchItems_CourseProgressId] ON [dbo].[BK_HomeworkWatchItems] ([CourseProgressId]);
END;
GO

-- 16. BK_LineNotificationLogs (LINE 推播紀錄與冪等防重送表)
IF OBJECT_ID(N'dbo.BK_LineNotificationLogs', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[BK_LineNotificationLogs] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RecipientParentUserId] INT NOT NULL,
        [LineUserId] NVARCHAR(100) NOT NULL,
        [NotificationType] NVARCHAR(50) NOT NULL,
        [IdempotencyKey] NVARCHAR(120) NOT NULL,
        [MessageText] NVARCHAR(500) NOT NULL,
        [ActionUrl] NVARCHAR(250) NOT NULL,
        [Status] NVARCHAR(40) NOT NULL,
        [RetryCount] INT NOT NULL DEFAULT 0,
        [SentAtUtc] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BK_LineNotificationLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BK_LineNotificationLogs_BK_Users_RecipientParentUserId] FOREIGN KEY ([RecipientParentUserId]) REFERENCES [dbo].[BK_Users] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_BK_LineNotificationLogs_IdempotencyKey] ON [dbo].[BK_LineNotificationLogs] ([IdempotencyKey]);
    CREATE INDEX [IX_BK_LineNotificationLogs_RecipientParentUserId] ON [dbo].[BK_LineNotificationLogs] ([RecipientParentUserId]);
END;
GO
