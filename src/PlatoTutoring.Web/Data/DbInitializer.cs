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
            if (await db.Users.AnyAsync())
            {
                await BackfillStudentSchoolGradeClassAsync(db);
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

            if (await db.Users.AnyAsync())
            {
                await BackfillStudentSchoolGradeClassAsync(db);
                _isInitialized = true;
                return;
            }
        }

        // 1. 系統設定 (SystemSettings)
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "SmallGroupUnitPrice", Value = "500", Description = "小組指導單堂基準價格 (NT$)" },
            new SystemSetting { Key = "SmallGroupPackageSize", Value = "6", Description = "小組指導預設每組堂數" },
            new SystemSetting { Key = "CancelRefundHours", Value = "24", Description = "開課前免費取消退堂時限 (小時)" },
            new SystemSetting { Key = "WaitlistHoldMinutes", Value = "30", Description = "候補名額釋出保留分鐘數" },
            new SystemSetting { Key = "AttachmentRetentionDays", Value = "90", Description = "匯款截圖附件保存天數" },
            new SystemSetting { Key = "GroupNoShowPenaltyEnabled", Value = "false", Description = "團體課 30 天內爽約 3 次停權 14 天開關（預設關閉）" }
        );

        // 2. 五種到班目的 (AttendancePurposeOptions)
        db.AttendancePurposeOptions.AddRange(
            new AttendancePurposeOption { Name = "課業提問", SortOrder = 1, IsActive = true },
            new AttendancePurposeOption { Name = "作業輔導", SortOrder = 2, IsActive = true },
            new AttendancePurposeOption { Name = "考前複習", SortOrder = 3, IsActive = true },
            new AttendancePurposeOption { Name = "進度補課", SortOrder = 4, IsActive = true },
            new AttendancePurposeOption { Name = "錯題訂正", SortOrder = 5, IsActive = true }
        );

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
            DisplayName = "林媽媽（家長）",
            Role = UserRole.Parent,
            Phone = "0912-345-678",
            LineUserId = "U_LINE_PARENT_LIN_001"
        };
        var parentChen = new AppUser
        {
            Username = "parent.chen",
            PasswordHash = defaultPasswordHash,
            DisplayName = "陳爸爸（家長）",
            Role = UserRole.Parent,
            Phone = "0922-888-999",
            LineUserId = "U_LINE_PARENT_CHEN_002"
        };
        var teacherWang = new AppUser
        {
            Username = "teacher.wang",
            PasswordHash = defaultPasswordHash,
            DisplayName = "王老師（數學／理化）",
            Role = UserRole.Teacher,
            Phone = "0933-111-222"
        };
        var adminBoss = new AppUser
        {
            Username = adminUsername,
            PasswordHash = adminPasswordHash,
            DisplayName = "蘇主任（系統管理員）",
            Role = UserRole.Admin,
            Phone = "0900-000-001"
        };

        db.Users.AddRange(parentLin, parentChen, teacherWang, adminBoss);
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
            School = "敦化國中",
            Grade = "國三",
            ClassName = "A班",
            GradeClass = "國三A班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        var stuXiaomei = new Student
        {
            FullName = "林小美",
            School = "敦化國中",
            Grade = "國二",
            ClassName = "B班",
            GradeClass = "國二B班",
            EnrollmentDate = new DateOnly(2026, 10, 15) // 插班生
        };
        var stuChen = new Student
        {
            FullName = "陳宥廷",
            School = "介壽國中",
            Grade = "國三",
            ClassName = "A班",
            GradeClass = "國三A班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };

        db.Students.AddRange(stuXiaoming, stuXiaomei, stuChen);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.AddRange(
            new ParentStudentLink { ParentUserId = parentLin.Id, StudentId = stuXiaoming.Id, Relationship = "母親" },
            new ParentStudentLink { ParentUserId = parentLin.Id, StudentId = stuXiaomei.Id, Relationship = "母親" },
            new ParentStudentLink { ParentUserId = parentChen.Id, StudentId = stuChen.Id, Relationship = "父親" }
        );
        await db.SaveChangesAsync();

        // 6. 發放學期贈送堂數與儲值批次（建立完整可稽核流水帳）
        await ledgerService.GrantSemesterBonusAsync(stuXiaoming.Id, semester.Id, adminBoss.Id);
        await ledgerService.GrantSemesterBonusAsync(stuXiaomei.Id, semester.Id, adminBoss.Id);
        await ledgerService.GrantSemesterBonusAsync(stuChen.Id, semester.Id, adminBoss.Id);

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
        await bookingService.CreateBookingAsync(slot4Full.Id, stuChen.Id, parentChen.Id, "考前複習");
        await bookingService.JoinWaitlistAsync(slot4Full.Id, stuXiaomei.Id, parentLin.Id, "作業輔導");

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
END";
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch { }
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN School TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN Grade TEXT NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE BK_Students ADD COLUMN ClassName TEXT NOT NULL DEFAULT '';"); } catch { }
        }
        else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"School\" varchar(80) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"Grade\" varchar(40) NOT NULL DEFAULT '';"); } catch { }
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"BK_Students\" ADD COLUMN IF NOT EXISTS \"ClassName\" varchar(40) NOT NULL DEFAULT '';"); } catch { }
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
                if (string.IsNullOrWhiteSpace(s.School))
                {
                    s.School = s.FullName.StartsWith("陳") ? "介壽國中" : "敦化國中";
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
}
