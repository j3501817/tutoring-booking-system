using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;
using Xunit;

namespace PlatoTutoring.Tests;

public class ComprehensiveEnhancementTests
{
    private static (AppDbContext Db, CreditLedgerService Ledger, BookingService Booking, LineNotificationService Line, StudentParentLinkingService Linking) CreateServices()
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

        return (db, ledger, booking, line, linking);
    }

    [Fact]
    public async Task StudentParentLinkingService_BindsBidirectionally_ByPhones()
    {
        var (db, _, _, _, linking) = CreateServices();

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
        var (db, _, booking, _, _) = CreateServices();

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
        var (db, _, booking, _, _) = CreateServices();

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
        var (db, _, _, _, _) = CreateServices();
        db.SystemSettings.Add(new SystemSetting { Key = "LineBot:ChannelId", Value = "2009998888" });
        db.SaveChanges();

        Assert.Equal("2009998888", authService.GetLoginClientId(db));
        Assert.True(authService.IsLineLoginConfigured(db));
    }

    [Fact]
    public async Task SystemAdminCalendar_BuildsMonthlyGrid_WithHolidayMap()
    {
        var (db, ledger, booking, _, _) = CreateServices();
        var adminUser = new AppUser { Username = "admin_cal", PasswordHash = "pw", DisplayName = "管理員", Role = UserRole.Admin };
        db.Users.Add(adminUser);

        var holiday = new AcademicHoliday { Date = new DateOnly(2026, 10, 24), Name = "光復節休假", IsActive = true };
        db.AcademicHolidays.Add(holiday);
        await db.SaveChangesAsync();

        var model = new PlatoTutoring.Web.Pages.SystemAdmin.IndexModel(db, ledger, booking);
        await model.OnGetAsync(2026, 10);

        Assert.Equal(2026, model.CalendarYear);
        Assert.Equal(10, model.CalendarMonth);
        Assert.Equal(31, model.DaysInMonth);
        Assert.Equal(9, model.PrevMonth);
        Assert.Equal(11, model.NextMonth);

        Assert.True(model.HolidayMap.ContainsKey(new DateOnly(2026, 10, 24)));
        Assert.Equal("光復節休假", model.HolidayMap[new DateOnly(2026, 10, 24)].Name);
    }
}
