using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;

namespace PlatoTutoring.Web.Data;

public static class DbInitializer
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _isInitialized;

    public static async Task EnsureInitializedSafeAsync(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService)
    {
        if (_isInitialized) return;

        await InitLock.WaitAsync();
        try
        {
            if (_isInitialized) return;
            await SeedAsync(db, ledgerService, bookingService);
            _isInitialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    public static async Task SeedAsync(
        AppDbContext db,
        CreditLedgerService ledgerService,
        BookingService bookingService)
    {
        await db.Database.EnsureCreatedAsync();

        try
        {
            await EnsureStudentColumnsExistAsync(db);
            await EnsureFieldOptionsSeededAsync(db);
            if (await db.Users.AnyAsync())
            {
                await BackfillStudentSchoolGradeClassAsync(db);
                await EnsureCreditBatchesAndLedgerConsistentAsync(db);
                _isInitialized = true;
                return;
            }
        }
        catch
        {
            // 若共用之 Azure SQL 資料庫已存在其他非 BK_ 資料表，EnsureCreatedAsync 不會建表，改呼叫 CreateTablesAsync 建立 BK_ 資料表
            try
            {
                var creator = db.Database.GetService<IRelationalDatabaseCreator>();
                await creator.CreateTablesAsync();
            }
            catch
            {
                // 若部分資料表已由手動 SQL 建立則忽略重複建表例外
            }

            await EnsureStudentColumnsExistAsync(db);
            await EnsureFieldOptionsSeededAsync(db);

            if (await db.Users.AnyAsync())
            {
                await BackfillStudentSchoolGradeClassAsync(db);
                _isInitialized = true;
                return;
            }
        }

        // 1. 系統設定 (SystemSettings)
        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.AddRange(
                new SystemSetting { Key = "SmallGroupUnitPrice", Value = "500", Description = "小組指導單堂基準價格 (NT$)" },
                new SystemSetting { Key = "SmallGroupPackageSize", Value = "6", Description = "小組指導預設每組堂數" },
                new SystemSetting { Key = "CancelRefundHours", Value = "24", Description = "開課前免費取消退堂時限 (小時)" },
                new SystemSetting { Key = "WaitlistHoldMinutes", Value = "30", Description = "候補名額釋出保留分鐘數" },
                new SystemSetting { Key = "AttachmentRetentionDays", Value = "90", Description = "匯款截圖附件保存天數" },
                new SystemSetting { Key = "GroupNoShowPenaltyEnabled", Value = "false", Description = "團體課 30 天內爽約 3 次停權 14 天開關（預設關閉）" }
            );
        }

        // 2. 到班目的 (AttendancePurposeOptions)
        if (!await db.AttendancePurposeOptions.AnyAsync())
        {
            db.AttendancePurposeOptions.AddRange(
                new AttendancePurposeOption { Name = "作業補正", SortOrder = 1, IsActive = true },
                new AttendancePurposeOption { Name = "輔導未完", SortOrder = 2, IsActive = true },
                new AttendancePurposeOption { Name = "進度落差", SortOrder = 3, IsActive = true },
                new AttendancePurposeOption { Name = "補課確認", SortOrder = 4, IsActive = true },
                new AttendancePurposeOption { Name = "紮根陪伴", SortOrder = 5, IsActive = true },
                new AttendancePurposeOption { Name = "考前救急", SortOrder = 6, IsActive = true }
            );
        }

        var defaultPasswordHash = PasswordHelper.HashPassword(
            Environment.GetEnvironmentVariable("INITIAL_DEFAULT_PASSWORD") ?? "demo123");
        var adminUsername = Environment.GetEnvironmentVariable("INITIAL_ADMIN_USERNAME") ?? "admin";
        var adminPasswordHash = PasswordHelper.HashPassword(
            Environment.GetEnvironmentVariable("INITIAL_ADMIN_PASSWORD") ?? "demo123");

        // 3. 三種角色使用者 (家長、老師、管理員)
        var parentLin = new AppUser
        {
            Username = "parent.lin",
            PasswordHash = defaultPasswordHash,
            DisplayName = "林媽媽",
            Role = UserRole.Parent,
            Phone = "0912-345-678",
            LineUserId = "U_LINE_PARENT_LIN_001"
        };
        var parentChen = new AppUser
        {
            Username = "parent.chen",
            PasswordHash = defaultPasswordHash,
            DisplayName = "陳爸爸",
            Role = UserRole.Parent,
            Phone = "0922-888-999",
            LineUserId = "U_LINE_PARENT_CHEN_002"
        };
        var parentHuang = new AppUser
        {
            Username = "parent.huang",
            PasswordHash = defaultPasswordHash,
            DisplayName = "黃媽媽",
            Role = UserRole.Parent,
            Phone = "0933-222-111",
            LineUserId = "U_LINE_PARENT_HUANG_003"
        };
        var teacherWang = new AppUser
        {
            Username = "teacher.wang",
            PasswordHash = defaultPasswordHash,
            DisplayName = "王老師",
            Role = UserRole.Teacher,
            Phone = "0933-111-222",
            Subjects = "數學,理化",
            IsProxyAdmin = false
        };
        var adminBoss = new AppUser
        {
            Username = adminUsername,
            PasswordHash = adminPasswordHash,
            DisplayName = "蘇主任（系統管理員）",
            Role = UserRole.Admin,
            Phone = "0900-000-001"
        };

        db.Users.AddRange(parentLin, parentChen, parentHuang, teacherWang, adminBoss);
        await db.SaveChangesAsync();

        // 4. 學期與進度
        var semester = new Semester
        {
            Name = "115學年度第1學期",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2027, 1, 20),
            DefaultBonusCredits = 3,
            IsCurrent = true,
            IsEnded = false
        };
        db.Semesters.Add(semester);
        await db.SaveChangesAsync();

        var progressW7 = new CourseProgress
        {
            SemesterId = semester.Id,
            Code = "W07",
            Title = "第7週－二次函數與極值應用",
            GradeClass = "國三A班",
            WeekStartDate = new DateOnly(2026, 10, 12),
            WeekEndDate = new DateOnly(2026, 10, 25),
            IsCurrentProgress = true
        };
        var progressW8 = new CourseProgress
        {
            SemesterId = semester.Id,
            Code = "W08",
            Title = "第8週－圓的幾何性質與切線",
            GradeClass = "國三A班",
            WeekStartDate = new DateOnly(2026, 10, 26),
            WeekEndDate = new DateOnly(2026, 11, 1),
            IsCurrentProgress = false
        };
        db.CourseProgresses.AddRange(progressW7, progressW8);
        await db.SaveChangesAsync();

        // 5. 學生與家長關聯（林媽媽有 2 位孩子，測試家長切換多位孩子功能）
        var stuXiaoming = new Student
        {
            FullName = "林小明",
            School = "三民",
            Grade = "國三",
            ClassName = "A班",
            GradeClass = "國三A班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        var stuXiaomei = new Student
        {
            FullName = "林小美",
            School = "三民",
            Grade = "國二",
            ClassName = "B班",
            GradeClass = "國二B班",
            EnrollmentDate = new DateOnly(2026, 10, 15) // 插班生
        };
        var stuChen = new Student
        {
            FullName = "陳宥廷",
            School = "培英",
            Grade = "國三",
            ClassName = "A班",
            GradeClass = "國三A班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        var stuHuang = new Student
        {
            FullName = "黃子恆",
            School = "光華",
            Grade = "國三",
            ClassName = "精英班",
            GradeClass = "國三精英班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };

        db.Students.AddRange(stuXiaoming, stuXiaomei, stuChen, stuHuang);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.AddRange(
            new ParentStudentLink { ParentUserId = parentLin.Id, StudentId = stuXiaoming.Id, Relationship = "媽媽" },
            new ParentStudentLink { ParentUserId = parentLin.Id, StudentId = stuXiaomei.Id, Relationship = "媽媽" },
            new ParentStudentLink { ParentUserId = parentChen.Id, StudentId = stuChen.Id, Relationship = "爸爸" },
            new ParentStudentLink { ParentUserId = parentHuang.Id, StudentId = stuHuang.Id, Relationship = "媽媽" }
        );
        await db.SaveChangesAsync();

        // 6. 發放學期贈送堂數與儲值批次（建立完整可稽核流水帳）
        await ledgerService.GrantSemesterBonusAsync(stuXiaoming.Id, semester.Id, adminBoss.Id);
        await ledgerService.GrantSemesterBonusAsync(stuXiaomei.Id, semester.Id, adminBoss.Id);
        await ledgerService.GrantSemesterBonusAsync(stuChen.Id, semester.Id, adminBoss.Id);
        await ledgerService.GrantSemesterBonusAsync(stuHuang.Id, semester.Id, adminBoss.Id);

        await ledgerService.RecordTopUpAsync(
            stuHuang.Id,
            teacherWang.Id,
            amountNtd: 3000m,
            creditsAdded: 6,
            remittanceDate: new DateOnly(2026, 10, 7),
            accountLastFiveDigits: "29481",
            note: "第一期小組指導 6 堂儲值");

        await ledgerService.RecordTopUpAsync(
            stuXiaoming.Id,
            teacherWang.Id,
            amountNtd: 3000m,
            creditsAdded: 6,
            remittanceDate: new DateOnly(2026, 10, 5),
            accountLastFiveDigits: "58392",
            note: "第一期小組指導 6 堂儲值");

        await ledgerService.RecordTopUpAsync(
            stuXiaomei.Id,
            teacherWang.Id,
            amountNtd: 3000m,
            creditsAdded: 6,
            remittanceDate: new DateOnly(2026, 10, 15),
            accountLastFiveDigits: "58392",
            note: "插班生首期 6 堂儲值");

        await ledgerService.RecordTopUpAsync(
            stuChen.Id,
            teacherWang.Id,
            amountNtd: 3000m,
            creditsAdded: 6,
            remittanceDate: new DateOnly(2026, 10, 6),
            accountLastFiveDigits: "10488",
            note: "第一期小組指導 6 堂儲值");

        // 7. 建立可預約時段（含小組指導、團體輔導、滿額可測試候補時段）
        var baseUtc = DateTime.UtcNow.Date.AddDays(2);

        var slot1 = await bookingService.CreateSlotAsync(
            teacherWang.Id,
            progressW7.Id,
            "國三A班",
            SlotType.SmallGroup,
            baseUtc.AddHours(10), // 18:00 Taipei
            baseUtc.AddHours(11.5),
            maxCapacity: 4,
            isExtraSlot: false,
            notes: "國三二次函數小組指導（A組）");

        var slot2 = await bookingService.CreateSlotAsync(
            teacherWang.Id,
            progressW7.Id,
            "國三A班",
            SlotType.SmallGroup,
            baseUtc.AddDays(1).AddHours(10), // 次日 18:00 Taipei (可供同進度改期)
            baseUtc.AddDays(1).AddHours(11.5),
            maxCapacity: 4,
            isExtraSlot: true,
            notes: "段考前加開小組指導（B組）");

        var slot3Group = await bookingService.CreateSlotAsync(
            teacherWang.Id,
            progressW7.Id,
            "全班別",
            SlotType.GroupCounseling,
            baseUtc.AddDays(2).AddHours(9), // 17:00 Taipei
            baseUtc.AddDays(2).AddHours(11),
            maxCapacity: 12,
            isExtraSlot: false,
            notes: "週五常態團體自習與作業問答（免費不扣堂）");

        var slot4Full = await bookingService.CreateSlotAsync(
            teacherWang.Id,
            progressW7.Id,
            "國三A班",
            SlotType.SmallGroup,
            baseUtc.AddDays(3).AddHours(6), // 14:00 Taipei
            baseUtc.AddDays(3).AddHours(7.5),
            maxCapacity: 1,
            isExtraSlot: false,
            notes: "週末精緻一對一／滿額候補示範時段");

        // 讓陳宥廷先預約 slot4Full，使其滿額，並讓林小美示範候補保留 30 分鐘通知狀態
        await bookingService.CreateBookingAsync(slot4Full.Id, stuChen.Id, parentChen.Id, "考前救急");
        await bookingService.JoinWaitlistAsync(slot4Full.Id, stuXiaomei.Id, parentLin.Id, "作業補正");

        // 示範一筆作業不佳追蹤通知
        var lineSvc = new LineNotificationService(db);
        await lineSvc.FlagHomeworkPoorAsync(
            stuXiaoming.Id,
            teacherWang.Id,
            progressW7.Id,
            "第7週講義 p.12-15 應用題錯誤率偏高，建議預約小組指導訂正");
    }

    private static async Task EnsureStudentColumnsExistAsync(AppDbContext db)
    {
        if (!db.Database.IsRelational()) return;

        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            const string sql = @"
IF OBJECT_ID(N'[BK_Students]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('BK_Students', 'School') IS NULL
        ALTER TABLE [BK_Students] ADD [School] nvarchar(80) NOT NULL CONSTRAINT [DF_BK_Students_School] DEFAULT N'';
    IF COL_LENGTH('BK_Students', 'Grade') IS NULL
        ALTER TABLE [BK_Students] ADD [Grade] nvarchar(40) NOT NULL CONSTRAINT [DF_BK_Students_Grade] DEFAULT N'';
    IF COL_LENGTH('BK_Students', 'ClassName') IS NULL
        ALTER TABLE [BK_Students] ADD [ClassName] nvarchar(40) NOT NULL CONSTRAINT [DF_BK_Students_ClassName] DEFAULT N'';
    IF COL_LENGTH('BK_Students', 'Phone1') IS NULL
        ALTER TABLE [BK_Students] ADD [Phone1] nvarchar(40) NULL CONSTRAINT [DF_BK_Students_Phone1] DEFAULT N'';
    IF COL_LENGTH('BK_Students', 'Phone2') IS NULL
        ALTER TABLE [BK_Students] ADD [Phone2] nvarchar(40) NULL CONSTRAINT [DF_BK_Students_Phone2] DEFAULT N'';
    IF COL_LENGTH('BK_Students', 'Phone3') IS NULL
        ALTER TABLE [BK_Students] ADD [Phone3] nvarchar(40) NULL CONSTRAINT [DF_BK_Students_Phone3] DEFAULT N'';
END
IF OBJECT_ID(N'[BK_Users]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('BK_Users', 'Subjects') IS NULL
        ALTER TABLE [BK_Users] ADD [Subjects] nvarchar(200) NOT NULL CONSTRAINT [DF_BK_Users_Subjects] DEFAULT N'';
    IF COL_LENGTH('BK_Users', 'IsProxyAdmin') IS NULL
        ALTER TABLE [BK_Users] ADD [IsProxyAdmin] bit NOT NULL CONSTRAINT [DF_BK_Users_IsProxyAdmin] DEFAULT 0;
END
IF OBJECT_ID(N'[BK_FieldOptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [BK_FieldOptions] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Category] nvarchar(40) NOT NULL,
        [Value] nvarchar(80) NOT NULL,
        [ParentValue] nvarchar(80) NULL,
        [SortOrder] int NOT NULL CONSTRAINT [DF_BK_FieldOptions_SortOrder] DEFAULT 1,
        [IsActive] bit NOT NULL CONSTRAINT [DF_BK_FieldOptions_IsActive] DEFAULT 1,
        CONSTRAINT [PK_BK_FieldOptions] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_BK_FieldOptions_Category] ON [BK_FieldOptions] ([Category]);
END
IF OBJECT_ID(N'[BK_AcademicHolidays]', N'U') IS NULL
BEGIN
    CREATE TABLE [BK_AcademicHolidays] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Date] date NOT NULL,
        [Name] nvarchar(80) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_BK_AcademicHolidays_IsActive] DEFAULT 1,
        [CreatedAtUtc] datetime2 NOT NULL CONSTRAINT [DF_BK_AcademicHolidays_CreatedAtUtc] DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_BK_AcademicHolidays] PRIMARY KEY ([Id])
    );
END";
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { }
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN School TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN Grade TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN ClassName TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN Phone1 TEXT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN Phone2 TEXT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN Phone3 TEXT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN EnrollmentStatus INTEGER NOT NULL DEFAULT 1;"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Users ADD COLUMN Subjects TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Users ADD COLUMN IsProxyAdmin INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS BK_FieldOptions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Category TEXT NOT NULL,
    Value TEXT NOT NULL,
    ParentValue TEXT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 1,
    IsActive INTEGER NOT NULL DEFAULT 1
);");
            }
            catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS BK_AcademicHolidays (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL,
    Name TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL
);");
            }
            catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS BK_StudentStatusLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentId INTEGER NOT NULL,
    FromStatus INTEGER NOT NULL,
    ToStatus INTEGER NOT NULL,
    SemesterId INTEGER NULL,
    Note TEXT NOT NULL DEFAULT '',
    OperatorName TEXT NOT NULL DEFAULT '',
    CreatedAtUtc TEXT NOT NULL
);");
            }
            catch { }
        }
        else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"School\" varchar(80) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"Grade\" varchar(40) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"ClassName\" varchar(40) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"Phone1\" varchar(40) NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"Phone2\" varchar(40) NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"Phone3\" varchar(40) NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"EnrollmentStatus\" int NOT NULL DEFAULT 1;"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Users\" ADD COLUMN IF NOT EXISTS \"Subjects\" varchar(200) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Users\" ADD COLUMN IF NOT EXISTS \"IsProxyAdmin\" boolean NOT NULL DEFAULT false;"); } catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""BK_FieldOptions"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Category"" varchar(40) NOT NULL,
    ""Value"" varchar(80) NOT NULL,
    ""ParentValue"" varchar(80) NULL,
    ""SortOrder"" int NOT NULL DEFAULT 1,
    ""IsActive"" boolean NOT NULL DEFAULT true
);");
            }
            catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""BK_AcademicHolidays"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Date"" date NOT NULL,
    ""Name"" varchar(80) NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""CreatedAtUtc"" timestamptz NOT NULL DEFAULT now()
);");
            }
            catch { }
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS ""BK_StudentStatusLogs"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""StudentId"" int NOT NULL,
    ""FromStatus"" int NOT NULL,
    ""ToStatus"" int NOT NULL,
    ""SemesterId"" int NULL,
    ""Note"" varchar(250) NOT NULL DEFAULT '',
    ""OperatorName"" varchar(80) NOT NULL DEFAULT '',
    ""CreatedAtUtc"" timestamptz NOT NULL DEFAULT now()
);");
            }
            catch { }
        }
    }

    private static async Task EnsureFieldOptionsSeededAsync(AppDbContext db)
    {
        try
        {
            var defaultPurposes = new[] { "作業補正", "輔導未完", "進度落差", "補課確認", "紮根陪伴", "考前救急" };
            foreach (var (pName, idx) in defaultPurposes.Select((name, i) => (name, i + 1)))
            {
                var existing = await db.AttendancePurposeOptions.FirstOrDefaultAsync(p => p.Name == pName);
                if (existing == null)
                {
                    db.AttendancePurposeOptions.Add(new AttendancePurposeOption
                    {
                        Name = pName,
                        SortOrder = idx,
                        IsActive = true
                    });
                }
            }

            // 確保班別有 資優班、精英班、通識
            var defaultClasses = new[] { "資優班", "精英班", "通識" };
            foreach (var (cName, idx) in defaultClasses.Select((name, i) => (name, i + 1)))
            {
                var existing = await db.FieldOptions.FirstOrDefaultAsync(o => o.Category == FieldCategories.Class && o.Value == cName);
                if (existing == null)
                {
                    db.FieldOptions.Add(new FieldOption
                    {
                        Category = FieldCategories.Class,
                        Value = cName,
                        ParentValue = null,
                        SortOrder = idx,
                        IsActive = true
                    });
                }
            }

            if (await db.FieldOptions.AnyAsync())
            {
                await db.SaveChangesAsync();
                return;
            }

            var schools = new[] { "三民", "培英", "光華", "竹光", "育賢", "私校" };
            var grades = new[] { "國一", "國二", "國三" };
            var classes = new[] { "資優班", "精英班", "通識" };
            var subjects = new[] { "國文", "英文", "數學", "理化", "社會" };

            var items = new List<FieldOption>();

            for (var i = 0; i < schools.Length; i++)
            {
                items.Add(new FieldOption { Category = FieldCategories.School, Value = schools[i], SortOrder = i + 1, IsActive = true });
            }

            for (var i = 0; i < classes.Length; i++)
            {
                items.Add(new FieldOption
                {
                    Category = FieldCategories.Class,
                    Value = classes[i],
                    ParentValue = null,
                    SortOrder = i + 1,
                    IsActive = true
                });
            }

            for (var i = 0; i < grades.Length; i++)
            {
                items.Add(new FieldOption { Category = FieldCategories.Grade, Value = grades[i], SortOrder = i + 1, IsActive = true });
            }

            for (var i = 0; i < subjects.Length; i++)
            {
                items.Add(new FieldOption { Category = FieldCategories.Subject, Value = subjects[i], SortOrder = i + 1, IsActive = true });
            }

            db.FieldOptions.AddRange(items);
            await db.SaveChangesAsync();
        }
        catch
        {
            // 忽略重複或連線異常
        }
    }

    private static async Task BackfillStudentSchoolGradeClassAsync(AppDbContext db)
    {
        try
        {
            var students = await db.Students.ToListAsync();
            var updated = false;
            foreach (var s in students)
            {
                if (string.IsNullOrWhiteSpace(s.School) || s.School is "敦化國中" or "介壽國中")
                {
                    s.School = s.FullName.StartsWith("陳") ? "培英" : "三民";
                    updated = true;
                }
                if (string.IsNullOrWhiteSpace(s.Grade) && !string.IsNullOrWhiteSpace(s.GradeClass))
                {
                    if (s.GradeClass.Length >= 2)
                    {
                        s.Grade = s.GradeClass[..2];
                        s.ClassName = s.GradeClass.Length > 2 ? s.GradeClass[2..] : "A班";
                    }
                    else
                    {
                        s.Grade = s.GradeClass;
                        s.ClassName = "A班";
                    }
                    updated = true;
                }
            }

            var links = await db.ParentStudentLinks.ToListAsync();
            foreach (var link in links)
            {
                if (link.Relationship == "母親")
                {
                    link.Relationship = "媽媽";
                    updated = true;
                }
                else if (link.Relationship == "父親")
                {
                    link.Relationship = "爸爸";
                    updated = true;
                }
            }

            var teachers = await db.Users.Where(u => u.Role == UserRole.Teacher).ToListAsync();
            foreach (var t in teachers)
            {
                if (string.IsNullOrWhiteSpace(t.Subjects) && t.DisplayName.Contains("數學"))
                {
                    t.Subjects = "數學,理化";
                    updated = true;
                }
            }

            if (!await db.Users.AnyAsync(u => u.Username == "parent.huang"))
            {
                var defaultPwdHash = PasswordHelper.HashPassword(
                    Environment.GetEnvironmentVariable("INITIAL_DEFAULT_PASSWORD") ?? "demo123");
                var parentHuang = new AppUser
                {
                    Username = "parent.huang",
                    PasswordHash = defaultPwdHash,
                    DisplayName = "黃媽媽",
                    Role = UserRole.Parent,
                    Phone = "0933-222-111",
                    LineUserId = "U_LINE_PARENT_HUANG_003"
                };
                db.Users.Add(parentHuang);
                await db.SaveChangesAsync();

                var stuHuang = await db.Students.FirstOrDefaultAsync(s => s.FullName == "黃子恆");
                if (stuHuang != null)
                {
                    db.ParentStudentLinks.Add(new ParentStudentLink
                    {
                        ParentUserId = parentHuang.Id,
                        StudentId = stuHuang.Id,
                        Relationship = "媽媽"
                    });
                    await db.SaveChangesAsync();
                }
            }

            if (updated)
            {
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // 忽略回填例外
        }
    }

    private static async Task EnsureCreditBatchesAndLedgerConsistentAsync(AppDbContext db)
    {
        try
        {
            var students = await db.Students
                .Where(s => !s.IsDroppedOut && s.PaidCredits > 0)
                .ToListAsync();

            if (!students.Any()) return;

            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
            var teacherUser = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Teacher) ?? adminUser;
            var opId = teacherUser?.Id ?? adminUser?.Id ?? 1;
            var opName = teacherUser?.DisplayName ?? adminUser?.DisplayName ?? "系統管理員";

            var updated = false;

            foreach (var s in students)
            {
                var batchSum = await db.TopUpBatches
                    .Where(b => b.StudentId == s.Id)
                    .SumAsync(b => b.RemainingPaidCredits);

                if (s.PaidCredits > batchSum)
                {
                    var diff = s.PaidCredits - batchSum;
                    var newBatch = new TopUpBatch
                    {
                        StudentId = s.Id,
                        RecordedByTeacherId = opId,
                        AmountNtd = diff * 500m,
                        CreditsAdded = diff,
                        UnitPriceNtd = 500m,
                        RemainingPaidCredits = diff,
                        RemittanceDate = s.EnrollmentDate,
                        AccountLastFiveDigits = "AUTO",
                        Note = "系統同步期初儲值批次",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    db.TopUpBatches.Add(newBatch);
                    await db.SaveChangesAsync();
                    updated = true;

                    // 檢查流水帳付費堂數是否也同步
                    var ledgerPaidSum = await db.CreditLedgerEntries
                        .Where(e => e.StudentId == s.Id)
                        .SumAsync(e => (int?)e.PaidDelta) ?? 0;

                    if (s.PaidCredits > ledgerPaidSum)
                    {
                        var ledgerDiff = s.PaidCredits - ledgerPaidSum;
                        db.CreditLedgerEntries.Add(new CreditLedgerEntry
                        {
                            StudentId = s.Id,
                            TransactionType = LedgerTransactionType.TopUpPaid,
                            PaidDelta = ledgerDiff,
                            BonusDelta = 0,
                            PaidBalanceAfter = s.PaidCredits,
                            BonusBalanceAfter = s.BonusCredits,
                            RelatedTopUpBatchId = newBatch.Id,
                            ReferenceNote = $"系統期初儲值批次同步補入 +{ledgerDiff} 堂",
                            OperatorUserId = opId,
                            OperatorName = opName,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                await db.SaveChangesAsync();
            }
        }
        catch
        {
            // 忽略同步例外
        }
    }
}

