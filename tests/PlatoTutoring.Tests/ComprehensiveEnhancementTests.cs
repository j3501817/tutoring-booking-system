using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;
using Xunit;

namespace PlatoTutoring.Tests;

public class ComprehensiveEnhancementTests
{
    private static (AppDbContext Db, CreditLedgerService Ledger, BookingService Booking, LineNotificationService Line, StudentParentLinkingService Linking, DbContextOptions<AppDbContext> Options) CreateServices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);

        db.SystemSettings.AddRange(
            new SystemSetting { Key = "GroupNoShowPenaltyEnabled", Value = "false", Description = "爽約停權開關" }
        );

        db.AttendancePurposeOptions.AddRange(
            new AttendancePurposeOption { Name = "作業補正", SortOrder = 1, IsActive = true },
            new AttendancePurposeOption { Name = "輔導未完", SortOrder = 2, IsActive = true },
            new AttendancePurposeOption { Name = "進度落差", SortOrder = 3, IsActive = true },
            new AttendancePurposeOption { Name = "補課確認", SortOrder = 4, IsActive = true },
            new AttendancePurposeOption { Name = "紮根陪伴", SortOrder = 5, IsActive = true },
            new AttendancePurposeOption { Name = "考前救急", SortOrder = 6, IsActive = true }
        );

        db.SaveChanges();

        var ledger = new CreditLedgerService(db);
        var line = new LineNotificationService(db);
        var booking = new BookingService(db, ledger, line);
        var linking = new StudentParentLinkingService(db);

        return (db, ledger, booking, line, linking, options);
    }

    [Fact]
    public async Task StudentParentLinkingService_BindsBidirectionally_ByPhones()
    {
        var (db, _, _, _, linking, _) = CreateServices();

        // 1. 先建立家長（電話：0912-345-678）
        var parentUser = new AppUser
        {
            Username = "parent_test",
            PasswordHash = "hash",
            DisplayName = "王大明家長",
            Role = UserRole.Parent,
            Phone = "0912-345-678"
        };
        db.Users.Add(parentUser);

        // 2. 建立學生（Phone2 輸入 0912-345-678）
        var student = new Student
        {
            FullName = "王小明",
            GradeClass = "國二A",
            Phone1 = "0900-111-222",
            Phone2 = "0912-345-678",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 3. 觸發學生電話比對綁定
        var linkedCount = await linking.AutoLinkStudentByPhonesAsync(student.Id);
        Assert.Equal(1, linkedCount);

        var link = await db.ParentStudentLinks.FirstOrDefaultAsync(l => l.StudentId == student.Id);
        Assert.NotNull(link);
        Assert.Equal(parentUser.Id, link.ParentUserId);

        // 4. 反向測試：建立新學生（Phone3：0988-777-666），此時無家長
        var student2 = new Student
        {
            FullName = "李小華",
            GradeClass = "國三B",
            Phone3 = "0988-777-666",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        db.Students.Add(student2);
        await db.SaveChangesAsync();
        await linking.AutoLinkStudentByPhonesAsync(student2.Id);

        // 尚未有對應家長
        var link2 = await db.ParentStudentLinks.FirstOrDefaultAsync(l => l.StudentId == student2.Id);
        Assert.Null(link2);

        // 5. 家長註冊並輸入電話 0988-777-666
        var parentUser2 = new AppUser
        {
            Username = "parent_lee",
            PasswordHash = "hash",
            DisplayName = "李小華家長",
            Role = UserRole.Parent,
            Phone = "0988-777-666"
        };
        db.Users.Add(parentUser2);
        await db.SaveChangesAsync();

        var parentLinkedCount = await linking.AutoLinkParentByPhoneAsync(parentUser2.Id);
        Assert.Equal(1, parentLinkedCount);

        link2 = await db.ParentStudentLinks.FirstOrDefaultAsync(l => l.StudentId == student2.Id);
        Assert.NotNull(link2);
        Assert.Equal(parentUser2.Id, link2.ParentUserId);
    }

    [Fact]
    public async Task BookingService_RejectsBooking_Within30MinutesOfStartTime()
    {
        var (db, _, booking, _, _, _) = CreateServices();

        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師", Role = UserRole.Teacher };
        var parent = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長", Role = UserRole.Parent };
        db.Users.AddRange(teacher, parent);

        var student = new Student { FullName = "測試生", GradeClass = "國一", PaidCredits = 5 };
        db.Students.Add(student);

        var progress = new CourseProgress { SemesterId = 1, Code = "W01", Title = "進度1", GradeClass = "國一", IsCurrentProgress = true };
        db.CourseProgresses.Add(progress);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parent.Id, StudentId = student.Id });

        var now = DateTime.UtcNow;
        // 設定時段：20 分鐘後開始（未滿 30 分鐘截止線）
        var slot = new TutoringSlot
        {
            TeacherUserId = teacher.Id,
            CourseProgressId = progress.Id,
            GradeClass = "國一",
            SlotType = SlotType.SmallGroup,
            StartsAtUtc = now.AddMinutes(20),
            EndsAtUtc = now.AddMinutes(140),
            MaxCapacity = 4,
            CurrentBookedCount = 0
        };
        db.TutoringSlots.Add(slot);
        await db.SaveChangesAsync();

        // 嘗試預約應拋出例外
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await booking.CreateBookingAsync(slot.Id, student.Id, parent.Id, "作業補正", now);
        });

        Assert.Contains("開課前 30 分鐘", ex.Message);
    }

    [Fact]
    public async Task BookingService_RejectsBooking_OnAcademicHoliday()
    {
        var (db, _, booking, _, _, _) = CreateServices();

        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師", Role = UserRole.Teacher };
        var parent = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長", Role = UserRole.Parent };
        db.Users.AddRange(teacher, parent);

        var student = new Student { FullName = "測試生", GradeClass = "國一", PaidCredits = 5 };
        db.Students.Add(student);

        var progress = new CourseProgress { SemesterId = 1, Code = "W01", Title = "進度1", GradeClass = "國一", IsCurrentProgress = true };
        db.CourseProgresses.Add(progress);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parent.Id, StudentId = student.Id });

        var now = DateTime.UtcNow;
        var slotTime = now.AddDays(3);
        var slotTaipeiDate = DateOnly.FromDateTime(BookingService.ToTaipeiTime(slotTime));

        // 設定該天為休假日
        db.AcademicHolidays.Add(new AcademicHoliday
        {
            Date = slotTaipeiDate,
            Name = "中秋連假",
            IsActive = true
        });

        var slot = new TutoringSlot
        {
            TeacherUserId = teacher.Id,
            CourseProgressId = progress.Id,
            GradeClass = "國一",
            SlotType = SlotType.SmallGroup,
            StartsAtUtc = slotTime,
            EndsAtUtc = slotTime.AddHours(2),
            MaxCapacity = 4,
            CurrentBookedCount = 0
        };
        db.TutoringSlots.Add(slot);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await booking.CreateBookingAsync(slot.Id, student.Id, parent.Id, "作業補正", now);
        });

        Assert.Contains("休假日", ex.Message);
    }

    [Fact]
    public void LineAuthService_DetectsMessagingApi_AndReadsSettings()
    {
        var configDict = new Dictionary<string, string?>
        {
            { "LineBot:ChannelAccessToken", "mock_token_123" },
            { "LineBot:ChannelSecret", "mock_secret_456" }
        };
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var authService = new LineAuthService(
            config,
            new HttpClient(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LineAuthService>.Instance);

        Assert.True(authService.IsMessagingConfigured());
        Assert.Equal("mock_token_123", authService.GetChannelAccessToken());
        Assert.Equal("mock_secret_456", authService.GetLoginClientSecret());

        // 尚未在 config 設定 ChannelId，但可透過 db SystemSettings 回退讀取
        var (db, _, _, _, _, _) = CreateServices();
        db.SystemSettings.Add(new SystemSetting { Key = "LineBot:ChannelId", Value = "2009998888" });
        db.SaveChanges();

        Assert.Equal("2009998888", authService.GetLoginClientId(db));
        Assert.True(authService.IsLineLoginConfigured(db));
    }

    [Fact]
    public async Task SystemAdminCalendar_BuildsMonthlyGrid_WithHolidayMap()
    {
        var (db, ledger, booking, line, _, options) = CreateServices();
        var adminUser = new AppUser { Username = "admin_cal", PasswordHash = "pw", DisplayName = "管理員", Role = UserRole.Admin };
        db.Users.Add(adminUser);

        var holiday = new AcademicHoliday { Date = new DateOnly(2026, 10, 24), Name = "光復節休假", IsActive = true };
        db.AcademicHolidays.Add(holiday);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddScoped(sp => new CreditLedgerService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new LineNotificationService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new BookingService(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<CreditLedgerService>(), sp.GetRequiredService<LineNotificationService>()));
        var sp = services.BuildServiceProvider();

        var scheduleManager = new ScheduleManagerService(sp, Microsoft.Extensions.Logging.Abstractions.NullLogger<ScheduleManagerService>.Instance);

        var model = new PlatoTutoring.Web.Pages.SystemAdmin.IndexModel(db, ledger, booking, scheduleManager);
        await model.OnGetAsync(2026, 10);

        Assert.Equal(2026, model.CalendarYear);
        Assert.Equal(10, model.CalendarMonth);
        Assert.Equal(31, model.DaysInMonth);
        Assert.Equal(9, model.PrevMonth);
        Assert.Equal(11, model.NextMonth);

        Assert.True(model.HolidayMap.ContainsKey(new DateOnly(2026, 10, 24)));
        Assert.Equal("光復節休假", model.HolidayMap[new DateOnly(2026, 10, 24)].Name);

        // 驗證排程管理功能
        Assert.NotEmpty(model.ScheduledTasks);
        Assert.Contains(model.ScheduledTasks, t => t.JobCode == "BonusExpiry");
        Assert.Contains(model.ScheduledTasks, t => t.JobCode == "WaitlistCleanup");

        var (success, msg) = await scheduleManager.TriggerJobAsync("BonusExpiry");
        Assert.True(success, msg);
        Assert.Contains("成功", msg);
    }

    [Fact]
    public async Task ScheduleManager_SupportsToggling_IntervalCustomization_AndPerJobRecentLogs()
    {
        var (db, ledger, booking, line, _, options) = CreateServices();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddScoped(sp => new CreditLedgerService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new LineNotificationService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new BookingService(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<CreditLedgerService>(), sp.GetRequiredService<LineNotificationService>()));
        var sp = services.BuildServiceProvider();

        var scheduleManager = new ScheduleManagerService(sp, Microsoft.Extensions.Logging.Abstractions.NullLogger<ScheduleManagerService>.Instance);

        // 1. 驗證各任務初始狀態
        var tasks = scheduleManager.GetScheduledTasks();
        Assert.Equal(4, tasks.Count);

        var waitlistTask = tasks.First(t => t.JobCode == "WaitlistCleanup");
        Assert.True(waitlistTask.IsEnabled);
        Assert.Equal(ScheduleFrequencyType.IntervalMinutes, waitlistTask.FrequencyType);
        Assert.Equal(5, waitlistTask.IntervalMinutes);
        Assert.NotEmpty(waitlistTask.RecentLogs);
        Assert.True(waitlistTask.RecentLogs.Count <= 3);

        // 2. 測試切換排程開關 (停用)
        await scheduleManager.ToggleTaskAsync("WaitlistCleanup", false);
        var updatedTasks = scheduleManager.GetScheduledTasks();
        var disabledTask = updatedTasks.First(t => t.JobCode == "WaitlistCleanup");
        Assert.False(disabledTask.IsEnabled);
        Assert.Null(disabledTask.NextRunAtUtc);
        Assert.Equal("已停用", disabledTask.Status);

        // 3. 測試自訂排程時間（自訂為每 1 分鐘間隔執行）
        await scheduleManager.UpdateTaskConfigAsync("WaitlistCleanup", true, ScheduleFrequencyType.IntervalMinutes, "00:00", 1);
        var interval1Tasks = scheduleManager.GetScheduledTasks();
        var reEnabledTask = interval1Tasks.First(t => t.JobCode == "WaitlistCleanup");
        Assert.True(reEnabledTask.IsEnabled);
        Assert.Equal(1, reEnabledTask.IntervalMinutes);
        Assert.NotNull(reEnabledTask.NextRunAtUtc);
        Assert.Equal("每 1 分鐘", reEnabledTask.ScheduleInterval);

        // 4. 測試設定每日固定時間 (例如每日 08:30)
        await scheduleManager.UpdateTaskConfigAsync("LessonReminder", true, ScheduleFrequencyType.DailyTime, "08:30", 1440);
        var lessonTasks = scheduleManager.GetScheduledTasks();
        var lessonTask = lessonTasks.First(t => t.JobCode == "LessonReminder");
        Assert.Equal("08:30", lessonTask.DailyTime);
        Assert.Equal("每日 08:30 (台北時間)", lessonTask.ScheduleInterval);
        Assert.NotNull(lessonTask.NextRunAtUtc);

        // 5. 測試觸發工作與每筆排程保留近 3 筆歷史
        for (int i = 0; i < 4; i++)
        {
            scheduleManager.RecordExecution("WaitlistCleanup", true, $"測試執行 {i}", TimeSpan.FromMilliseconds(50));
        }
        var recentLogs = scheduleManager.GetScheduledTasks().First(t => t.JobCode == "WaitlistCleanup").RecentLogs;
        Assert.Equal(3, recentLogs.Count); // 最多只保留近 3 筆
        Assert.Equal("測試執行 3", recentLogs[0].Message);
    }

    [Fact]
    public async Task StudentLifecycle_UnrenewedAndRenew_GrantsBonusAndLogsHistory()
    {
        var (db, ledger, _, _, _, _) = CreateServices();

        var admin = new AppUser { Username = "admin_test", DisplayName = "主任", Role = UserRole.Admin };
        db.Users.Add(admin);

        var sem = new Semester
        {
            Name = "115下學期",
            StartDate = new DateOnly(2026, 2, 1),
            EndDate = new DateOnly(2026, 6, 30),
            IsCurrent = true,
            DefaultBonusCredits = 3
        };
        db.Semesters.Add(sem);

        var student = new Student
        {
            FullName = "林小明",
            GradeClass = "國二A班",
            EnrollmentDate = new DateOnly(2026, 1, 15),
            EnrollmentStatus = StudentEnrollmentStatus.Enrolled,
            PaidCredits = 10,
            BonusCredits = 0
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 1. 標為未續報
        await ledger.MarkStudentUnrenewedAsync(student.Id, admin.Id, "家長評估中");
        Assert.Equal(StudentEnrollmentStatus.Unrenewed, student.EnrollmentStatus);
        Assert.Equal(10, student.PaidCredits); // 堂數保留

        // 檢查歷程紀錄
        var log1 = await db.StudentStatusLogs.FirstOrDefaultAsync(l => l.StudentId == student.Id);
        Assert.NotNull(log1);
        Assert.Equal(StudentEnrollmentStatus.Enrolled, log1.FromStatus);
        Assert.Equal(StudentEnrollmentStatus.Unrenewed, log1.ToStatus);
        Assert.Equal("家長評估中", log1.Note);

        // 2. 辦理續報（恢復在學，並自動補發本期贈堂）
        var (renewedStudent, bonusGranted) = await ledger.RenewStudentAsync(student.Id, admin.Id, "家長確認續報");
        Assert.Equal(StudentEnrollmentStatus.Enrolled, renewedStudent.EnrollmentStatus);
        Assert.Equal(3, bonusGranted);
        Assert.Equal(3, renewedStudent.BonusCredits);

        // 檢查第二次續報不會重複補發（防呆去重）
        var (_, bonusSecond) = await ledger.RenewStudentAsync(student.Id, admin.Id, "再次確認");
        Assert.Equal(0, bonusSecond);
        Assert.Equal(3, renewedStudent.BonusCredits);
    }

    [Fact]
    public async Task BookingService_BlocksUnrenewedOrDroppedOutStudents()
    {
        var (db, ledger, booking, _, _, _) = CreateServices();

        var teacher = new AppUser { Username = "t1", DisplayName = "張老師", Role = UserRole.Teacher };
        db.Users.Add(teacher);

        var sem = new Semester { Name = "115下", StartDate = new DateOnly(2026, 2, 1), EndDate = new DateOnly(2026, 6, 30), IsCurrent = true };
        db.Semesters.Add(sem);

        var progress = new CourseProgress { SemesterId = sem.Id, Code = "P1", Title = "第一單元", GradeClass = "國二A班", IsCurrentProgress = true };
        db.CourseProgresses.Add(progress);
        await db.SaveChangesAsync();

        var slot = await booking.CreateSlotAsync(teacher.Id, progress.Id, "國二A班", SlotType.SmallGroup,
            DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(1), 3, false, "課表");

        var student = new Student
        {
            FullName = "王大寶",
            GradeClass = "國二A班",
            EnrollmentDate = new DateOnly(2026, 2, 1),
            EnrollmentStatus = StudentEnrollmentStatus.Unrenewed,
            PaidCredits = 5,
            BonusCredits = 0
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 未續報學生預約應被阻擋
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.CreateBookingAsync(slot.Id, student.Id, teacher.Id, "作業補正"));
        Assert.Contains("尚未完成續報", ex.Message);

        // 未續報學生候補也應被阻擋
        var exWait = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.JoinWaitlistAsync(slot.Id, student.Id, teacher.Id, "作業補正"));
        Assert.Contains("尚未完成續報", exWait.Message);
    }

    [Fact]
    public async Task SemesterSwitch_GrantsBonusToActiveEnrolledStudentsOnly()
    {
        var (db, ledger, _, _, _, _) = CreateServices();
        var admin = new AppUser { Username = "admin2", DisplayName = "蘇主任", Role = UserRole.Admin };
        db.Users.Add(admin);

        var sem1 = new Semester { Name = "114下學期", StartDate = new DateOnly(2025, 2, 1), EndDate = new DateOnly(2025, 6, 30), IsCurrent = false, DefaultBonusCredits = 3 };
        var sem2 = new Semester { Name = "115上學期", StartDate = new DateOnly(2025, 9, 1), EndDate = new DateOnly(2026, 1, 20), IsCurrent = true, DefaultBonusCredits = 4 };
        db.Semesters.AddRange(sem1, sem2);

        // 學生 A: 在學
        var stuActive = new Student { FullName = "在學同學", GradeClass = "國二", EnrollmentDate = new DateOnly(2025, 1, 1), EnrollmentStatus = StudentEnrollmentStatus.Enrolled, BonusCredits = 0 };
        // 學生 B: 未續報
        var stuUnrenewed = new Student { FullName = "未續報同學", GradeClass = "國二", EnrollmentDate = new DateOnly(2025, 1, 1), EnrollmentStatus = StudentEnrollmentStatus.Unrenewed, BonusCredits = 0 };
        // 學生 C: 退班
        var stuDropped = new Student { FullName = "退班同學", GradeClass = "國二", EnrollmentDate = new DateOnly(2025, 1, 1), EnrollmentStatus = StudentEnrollmentStatus.DroppedOut, IsDroppedOut = true, BonusCredits = 0 };

        db.Students.AddRange(stuActive, stuUnrenewed, stuDropped);
        await db.SaveChangesAsync();

        // 執行新學期全體在籍學生自動補發
        var grantedCount = await ledger.GrantSemesterBonusToActiveStudentsAsync(sem2.Id, admin.Id);

        Assert.Equal(1, grantedCount);
        Assert.Equal(4, stuActive.BonusCredits);
        Assert.Equal(0, stuUnrenewed.BonusCredits);
        Assert.Equal(0, stuDropped.BonusCredits);
    }

    [Fact]
    public async Task SemesterManagement_EnforcesSingleCurrentSemester_Correctly()
    {
        var (db, ledger, booking, line, _, options) = CreateServices();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddScoped(sp => new CreditLedgerService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new LineNotificationService(sp.GetRequiredService<AppDbContext>()));
        services.AddScoped(sp => new BookingService(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<CreditLedgerService>(), sp.GetRequiredService<LineNotificationService>()));
        var sp = services.BuildServiceProvider();
        var scheduleManager = new ScheduleManagerService(sp, Microsoft.Extensions.Logging.Abstractions.NullLogger<ScheduleManagerService>.Instance);

        var pageModel = new PlatoTutoring.Web.Pages.SystemAdmin.IndexModel(db, ledger, booking, scheduleManager);
        db.Users.Add(new AppUser { Username = "admin_sem", PasswordHash = "pw", DisplayName = "系統管理員", Role = UserRole.Admin });
        await db.SaveChangesAsync();

        // 1. 新增第一個學期：115 學年 第一學期 (設為目前學期)
        await pageModel.OnPostSaveSemesterAsync(null, "115-1 秋季學期", DateTime.Parse("2026-09-01"), DateTime.Parse("2027-01-20"), setAsCurrent: true);
        var sem1 = await db.Semesters.FirstAsync(s => s.Name == "115-1 秋季學期");
        Assert.True(sem1.IsCurrent);

        // 2. 新增第二個學期：115 學年 第二學期 (設為目前學期)
        await pageModel.OnPostSaveSemesterAsync(null, "115-2 春季學期", DateTime.Parse("2027-02-15"), DateTime.Parse("2027-06-30"), setAsCurrent: true);
        var sem2 = await db.Semesters.FirstAsync(s => s.Name == "115-2 春季學期");
        
        // 重新讀取 sem1
        await db.Entry(sem1).ReloadAsync();
        Assert.True(sem2.IsCurrent);
        Assert.False(sem1.IsCurrent); // 第一學期自動被切換為 false！
        Assert.Equal(1, await db.Semesters.CountAsync(s => s.IsCurrent));

        // 3. 測試切換回第一學期
        await pageModel.OnPostSwitchSemesterAsync(sem1.Id);
        await db.Entry(sem1).ReloadAsync();
        await db.Entry(sem2).ReloadAsync();
        Assert.True(sem1.IsCurrent);
        Assert.False(sem2.IsCurrent);
        Assert.Equal(1, await db.Semesters.CountAsync(s => s.IsCurrent));

        // 4. 測試防呆：編輯唯一目前學期時嘗試取消勾選 (setAsCurrent: false)
        await pageModel.OnPostSaveSemesterAsync(sem1.Id, "115-1 秋季學期", DateTime.Parse("2026-09-01"), DateTime.Parse("2027-01-20"), setAsCurrent: false);
        await db.Entry(sem1).ReloadAsync();
        Assert.True(sem1.IsCurrent); // 觸發防呆保護，強制維持唯一進行中學期！
        Assert.Equal(1, await db.Semesters.CountAsync(s => s.IsCurrent));
    }

    [Fact]
    public async Task PreviewDropoutSettlement_AutoReconciles_WhenStudentHasUnbatchedPaidCredits()
    {
        var (db, ledger, _, _, _, _) = CreateServices();
        var admin = new AppUser { Username = "admin_settle", DisplayName = "主任", Role = UserRole.Admin };
        db.Users.Add(admin);

        // 學生帳戶在主檔有 10 堂付費堂數，但儲值批次只有 1 筆 4 堂（缺 6 堂批次資料）
        var student = new Student
        {
            FullName = "資料缺漏生",
            PaidCredits = 10,
            BonusCredits = 2,
            GradeClass = "國二A班",
            EnrollmentDate = new DateOnly(2026, 9, 1),
            EnrollmentStatus = StudentEnrollmentStatus.Enrolled
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var batch = new TopUpBatch
        {
            StudentId = student.Id,
            RecordedByTeacherId = admin.Id,
            AmountNtd = 2400m,
            CreditsAdded = 4,
            UnitPriceNtd = 600m,
            RemainingPaidCredits = 4,
            RemittanceDate = new DateOnly(2026, 9, 1),
            AccountLastFiveDigits = "12345",
            Note = "首批 4 堂",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.TopUpBatches.Add(batch);
        await db.SaveChangesAsync();

        // 試算退班結算
        var preview = await ledger.PreviewDropoutSettlementAsync(student.Id);

        // 驗證：可退付費堂數必須與學生主檔可用付費堂數 (10 堂) 完全一致！
        Assert.Equal(10, preview.TotalRefundablePaidCredits);
        // 金額：4 * 600 (2400) + 6 * 600 (3600) = 6000
        Assert.Equal(6000m, preview.TotalRefundAmountNtd);
        Assert.Equal(2, preview.RemainingBonusCreditsToForfeit);
        Assert.Equal(2, preview.BatchBreakdowns.Count);

        // 執行退班結算
        var settlement = await ledger.ExecuteDropoutSettlementAsync(student.Id, admin.Id, "測試退班");
        await db.Entry(student).ReloadAsync();

        Assert.Equal(0, student.PaidCredits);
        Assert.Equal(0, student.BonusCredits);
        Assert.True(student.IsDroppedOut);
        Assert.Equal(StudentEnrollmentStatus.DroppedOut, student.EnrollmentStatus);
        Assert.Equal(10, settlement.TotalRefundablePaidCredits);
        Assert.Equal(6000m, settlement.CalculatedRefundAmountNtd);
    }

    [Fact]
    public async Task DbInitializer_SeedsThreeParents_ForQuickSwitch()
    {
        var (db, ledger, booking, _, _, _) = CreateServices();
        await DbInitializer.SeedAsync(db, ledger, booking);

        var pLin = await db.Users.FirstOrDefaultAsync(u => u.Username == "parent.lin");
        var pChen = await db.Users.FirstOrDefaultAsync(u => u.Username == "parent.chen");
        var pHuang = await db.Users.FirstOrDefaultAsync(u => u.Username == "parent.huang");

        Assert.NotNull(pLin);
        Assert.Equal(UserRole.Parent, pLin.Role);
        Assert.Equal("林媽媽", pLin.DisplayName);

        Assert.NotNull(pChen);
        Assert.Equal(UserRole.Parent, pChen.Role);
        Assert.Equal("陳爸爸", pChen.DisplayName);

        Assert.NotNull(pHuang);
        Assert.Equal(UserRole.Parent, pHuang.Role);
        Assert.Equal("黃媽媽", pHuang.DisplayName);

        // 驗證三位家長皆有關聯孩子
        Assert.True(await db.ParentStudentLinks.AnyAsync(l => l.ParentUserId == pLin.Id));
        Assert.True(await db.ParentStudentLinks.AnyAsync(l => l.ParentUserId == pChen.Id));
        Assert.True(await db.ParentStudentLinks.AnyAsync(l => l.ParentUserId == pHuang.Id));
    }

    [Fact]
    public async Task BookingService_UnrenewedOrDroppedOutStudent_CannotBookOrJoinWaitlist()
    {
        var (db, _, booking, _, _, _) = CreateServices();

        var teacher = new AppUser { Username = "t1", DisplayName = "老師", Role = UserRole.Teacher, PasswordHash = "h" };
        var parent = new AppUser { Username = "p1", DisplayName = "家長", Role = UserRole.Parent, PasswordHash = "h" };
        db.Users.AddRange(teacher, parent);
        await db.SaveChangesAsync();

        var studentUnrenewed = new Student
        {
            FullName = "未續報學生",
            EnrollmentStatus = StudentEnrollmentStatus.Unrenewed,
            IsDroppedOut = false,
            PaidCredits = 5,
            BonusCredits = 2
        };
        var studentDropped = new Student
        {
            FullName = "已退班學生",
            EnrollmentStatus = StudentEnrollmentStatus.DroppedOut,
            IsDroppedOut = true,
            PaidCredits = 0,
            BonusCredits = 0
        };
        db.Students.AddRange(studentUnrenewed, studentDropped);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.AddRange(
            new ParentStudentLink { ParentUserId = parent.Id, StudentId = studentUnrenewed.Id, Relationship = "家長" },
            new ParentStudentLink { ParentUserId = parent.Id, StudentId = studentDropped.Id, Relationship = "家長" }
        );

        var sem = new Semester { Name = "115-1", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)), IsCurrent = true };
        db.Semesters.Add(sem);
        await db.SaveChangesAsync();

        var prog = new CourseProgress { SemesterId = sem.Id, Code = "W01", Title = "測試進度", WeekStartDate = sem.StartDate, WeekEndDate = sem.StartDate.AddDays(6) };
        db.CourseProgresses.Add(prog);
        await db.SaveChangesAsync();

        var slot = new TutoringSlot
        {
            CourseProgressId = prog.Id,
            TeacherUserId = teacher.Id,
            GradeClass = "國二",
            SlotType = SlotType.SmallGroup,
            StartsAtUtc = DateTime.UtcNow.AddDays(2),
            EndsAtUtc = DateTime.UtcNow.AddDays(2).AddHours(2),
            MaxCapacity = 4,
            CurrentBookedCount = 0
        };
        db.TutoringSlots.Add(slot);
        await db.SaveChangesAsync();

        // 1. 未續報學生嘗試預約 -> 應拋出包含「續報」之例外
        var exBookUnrenewed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.CreateBookingAsync(slot.Id, studentUnrenewed.Id, parent.Id, "作業補正"));
        Assert.Contains("續報", exBookUnrenewed.Message);

        // 2. 已退班學生嘗試預約 -> 應拋出包含「續報」或「退班」之例外
        var exBookDropped = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.CreateBookingAsync(slot.Id, studentDropped.Id, parent.Id, "作業補正"));
        Assert.Contains("續報", exBookDropped.Message);

        // 3. 嘗試候補 -> 亦應拋出例外阻擋
        slot.CurrentBookedCount = slot.MaxCapacity;
        await db.SaveChangesAsync();

        var exWaitUnrenewed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.JoinWaitlistAsync(slot.Id, studentUnrenewed.Id, parent.Id, "作業補正"));
        Assert.Contains("續報", exWaitUnrenewed.Message);
    }

    [Fact]
    public async Task CancelWaitlistAsync_CancelsEntry_ReordersSubsequentEntries_AndLogsHistory()
    {
        var (db, ledger, booking, _, _, _) = CreateServices();

        var parent = new AppUser { Username = "p_test_cancel", DisplayName = "家長", Role = UserRole.Parent };
        db.Users.Add(parent);

        var s1 = new Student { FullName = "學生甲", EnrollmentStatus = StudentEnrollmentStatus.Enrolled };
        var s2 = new Student { FullName = "學生乙", EnrollmentStatus = StudentEnrollmentStatus.Enrolled };
        var s3 = new Student { FullName = "學生丙", EnrollmentStatus = StudentEnrollmentStatus.Enrolled };
        db.Students.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        var sem = new Semester { Name = "115-1", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)), IsCurrent = true };
        db.Semesters.Add(sem);
        await db.SaveChangesAsync();

        var prog = new CourseProgress { SemesterId = sem.Id, Code = "W02", Title = "進度測試", WeekStartDate = sem.StartDate, WeekEndDate = sem.StartDate.AddDays(6) };
        db.CourseProgresses.Add(prog);
        await db.SaveChangesAsync();

        var slot = new TutoringSlot
        {
            CourseProgressId = prog.Id,
            TeacherUserId = parent.Id,
            GradeClass = "國二",
            SlotType = SlotType.SmallGroup,
            StartsAtUtc = DateTime.UtcNow.AddDays(3),
            EndsAtUtc = DateTime.UtcNow.AddDays(3).AddHours(2),
            MaxCapacity = 4,
            CurrentBookedCount = 4 // 滿額
        };
        db.TutoringSlots.Add(slot);
        await db.SaveChangesAsync();

        // 加入三位候補
        var w1 = await booking.JoinWaitlistAsync(slot.Id, s1.Id, parent.Id, "作業補正");
        var w2 = await booking.JoinWaitlistAsync(slot.Id, s2.Id, parent.Id, "進度落差");
        var w3 = await booking.JoinWaitlistAsync(slot.Id, s3.Id, parent.Id, "紮根陪伴");

        Assert.Equal(1, w1.Position);
        Assert.Equal(2, w2.Position);
        Assert.Equal(3, w3.Position);

        // 取消中間的第 2 位（學生乙）
        var cancelled = await booking.CancelWaitlistAsync(w2.Id, parent.Id, "家長臨時有事取消");

        Assert.Equal(WaitlistStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Equal("家長臨時有事取消", cancelled.CancelReason);

        // 重新讀取 w1 與 w3
        var reW1 = await db.WaitlistEntries.FindAsync(w1.Id);
        var reW3 = await db.WaitlistEntries.FindAsync(w3.Id);

        Assert.NotNull(reW1);
        Assert.Equal(1, reW1.Position); // 順位 1 不受影響

        Assert.NotNull(reW3);
        Assert.Equal(2, reW3.Position); // 順位 3 自動向前遞補變為 2

        // 檢查歷程有寫入 LineNotificationLog
        var log = await db.LineNotificationLogs.FirstOrDefaultAsync(l => l.NotificationType == "WaitlistCancelled");
        Assert.NotNull(log);
        Assert.Contains("學生乙", log.MessageText);
        Assert.Contains("原第 #2 順位", log.MessageText);
    }
}

