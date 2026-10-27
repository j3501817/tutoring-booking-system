using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;
using PlatoTutoring.Web.Services;
using Xunit;

namespace PlatoTutoring.Tests;

public class BookingAndLedgerTests
{
    private static (AppDbContext Db, CreditLedgerService Ledger, BookingService Booking, LineNotificationService Line) CreateTestServices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);

        db.SystemSettings.AddRange(
            new SystemSetting { Key = "GroupNoShowPenaltyEnabled", Value = "false", Description = "爽約停權開關" }
        );

        db.AttendancePurposeOptions.AddRange(
            new AttendancePurposeOption { Name = "課業提問", SortOrder = 1, IsActive = true },
            new AttendancePurposeOption { Name = "作業輔導", SortOrder = 2, IsActive = true },
            new AttendancePurposeOption { Name = "考前複習", SortOrder = 3, IsActive = true },
            new AttendancePurposeOption { Name = "進度補課", SortOrder = 4, IsActive = true },
            new AttendancePurposeOption { Name = "錯題訂正", SortOrder = 5, IsActive = true }
        );

        db.SaveChanges();

        var ledger = new CreditLedgerService(db);
        var line = new LineNotificationService(db);
        var booking = new BookingService(db, ledger, line);

        return (db, ledger, booking, line);
    }

    [Fact]
    public async Task PriorityDeduction_UsesBonusFirst_ThenPaid_AndReconcilesCleanly()
    {
        var (db, ledger, booking, _) = CreateTestServices();

        var parent = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長A", Role = UserRole.Parent, LineUserId = "U1" };
        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師A", Role = UserRole.Teacher };
        var admin = new AppUser { Username = "a1", PasswordHash = "pw", DisplayName = "管理員A", Role = UserRole.Admin };
        db.Users.AddRange(parent, teacher, admin);

        var student = new Student { FullName = "測試學生", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parent.Id, StudentId = student.Id });
        var p1 = new CourseProgress { SemesterId = 1, Code = "W01", Title = "進度1", GradeClass = "國三A班", IsCurrentProgress = true };
        var p2 = new CourseProgress { SemesterId = 1, Code = "W02", Title = "進度2", GradeClass = "國三A班", IsCurrentProgress = true };
        db.CourseProgresses.AddRange(p1, p2);
        await db.SaveChangesAsync();

        // 老師發放 1 堂贈送堂數 + 登錄 6 堂付費儲值
        await ledger.ApplyManualAdjustmentAsync(student.Id, teacher.Id, paidDelta: 0, bonusDelta: 1, "入班贈送1堂");
        await ledger.RecordTopUpAsync(student.Id, teacher.Id, 3000m, 6, new DateOnly(2026, 10, 1), "12345", "首期儲值");

        var now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var slot1 = await booking.CreateSlotAsync(teacher.Id, p1.Id, "國三A班", SlotType.SmallGroup, now.AddDays(3), now.AddDays(3).AddHours(1.5), 4, false, "");
        var slot2 = await booking.CreateSlotAsync(teacher.Id, p2.Id, "國三A班", SlotType.SmallGroup, now.AddDays(4), now.AddDays(4).AddHours(1.5), 4, false, "");

        // 第 1 次預約：應優先扣贈送堂數 1 堂（剩餘贈送 0、付費 6）
        var b1 = await booking.CreateBookingAsync(slot1.Id, student.Id, parent.Id, "課業提問", now);
        Assert.Equal(1, b1.BonusCreditsUsed);
        Assert.Equal(0, b1.PaidCreditsUsed);
        Assert.Equal(0, student.BonusCredits);
        Assert.Equal(6, student.PaidCredits);

        // 第 2 次預約：贈送已為 0，應扣付費堂數 1 堂（剩餘贈送 0、付費 5）
        var b2 = await booking.CreateBookingAsync(slot2.Id, student.Id, parent.Id, "考前複習", now);
        Assert.Equal(0, b2.BonusCreditsUsed);
        Assert.Equal(1, b2.PaidCreditsUsed);
        Assert.Equal(0, student.BonusCredits);
        Assert.Equal(5, student.PaidCredits);

        // 驗證夜間對帳無任何誤差
        var discrepancies = await ledger.RunNightlyReconciliationAsync();
        Assert.Empty(discrepancies);
    }

    [Fact]
    public async Task Cancellation24HourBoundary_RefundsWhenAtLeast24Hours_NoRefundWhenUnder24Hours()
    {
        var (db, ledger, booking, _) = CreateTestServices();

        var parent = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長A", Role = UserRole.Parent };
        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師A", Role = UserRole.Teacher };
        db.Users.AddRange(parent, teacher);

        var student = new Student { FullName = "學生B", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parent.Id, StudentId = student.Id });

        var p1 = new CourseProgress { SemesterId = 1, Code = "W01", Title = "進度1", GradeClass = "國三A班", IsCurrentProgress = true };
        db.CourseProgresses.Add(p1);
        await db.SaveChangesAsync();

        await ledger.RecordTopUpAsync(student.Id, teacher.Id, 3000m, 6, new DateOnly(2026, 10, 1), "99999", "儲值");

        var slotStart = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        var slot = await booking.CreateSlotAsync(teacher.Id, p1.Id, "國三A班", SlotType.SmallGroup, slotStart, slotStart.AddHours(1.5), 4, false, "");

        // 在開課前 48 小時預約（剩 5 堂）
        var createdBooking = await booking.CreateBookingAsync(slot.Id, student.Id, parent.Id, "作業輔導", slotStart.AddHours(-48));
        Assert.Equal(5, student.PaidCredits);

        // 在開課前剛好 24 小時取消 → 應退還 1 堂（恢復為 6 堂）
        var (_, refundedOnTime) = await booking.CancelBookingAsync(createdBooking.Id, parent.Id, "準時取消", slotStart.AddHours(-24));
        Assert.True(refundedOnTime);
        Assert.Equal(6, student.PaidCredits);

        // 再次預約（剩 5 堂），並在開課前 23.5 小時取消 → 逾時不退堂（維持 5 堂）
        var secondBooking = await booking.CreateBookingAsync(slot.Id, student.Id, parent.Id, "作業輔導", slotStart.AddHours(-23.9));
        var (_, refundedLate) = await booking.CancelBookingAsync(secondBooking.Id, parent.Id, "臨時取消", slotStart.AddHours(-23.5));
        Assert.False(refundedLate);
        Assert.Equal(5, student.PaidCredits);
    }

    [Fact]
    public async Task WaitlistHold30Minutes_AutoRotatesWhenExpired_AndConfirmsWithinWindow()
    {
        var (db, ledger, booking, _) = CreateTestServices();

        var p1User = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長1", Role = UserRole.Parent, LineUserId = "U1" };
        var p2User = new AppUser { Username = "p2", PasswordHash = "pw", DisplayName = "家長2", Role = UserRole.Parent, LineUserId = "U2" };
        var p3User = new AppUser { Username = "p3", PasswordHash = "pw", DisplayName = "家長3", Role = UserRole.Parent, LineUserId = "U3" };
        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師", Role = UserRole.Teacher };
        db.Users.AddRange(p1User, p2User, p3User, teacher);

        var s1 = new Student { FullName = "學生1", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        var s2 = new Student { FullName = "學生2", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        var s3 = new Student { FullName = "學生3", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        db.Students.AddRange(s1, s2, s3);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.AddRange(
            new ParentStudentLink { ParentUserId = p1User.Id, StudentId = s1.Id },
            new ParentStudentLink { ParentUserId = p2User.Id, StudentId = s2.Id },
            new ParentStudentLink { ParentUserId = p3User.Id, StudentId = s3.Id }
        );

        var prog = new CourseProgress { SemesterId = 1, Code = "W01", Title = "進度1", GradeClass = "國三A班", IsCurrentProgress = true };
        db.CourseProgresses.Add(prog);
        await db.SaveChangesAsync();

        await ledger.RecordTopUpAsync(s1.Id, teacher.Id, 500m, 1, new DateOnly(2026, 10, 1), "11111", "");
        await ledger.RecordTopUpAsync(s2.Id, teacher.Id, 500m, 1, new DateOnly(2026, 10, 1), "22222", "");
        await ledger.RecordTopUpAsync(s3.Id, teacher.Id, 500m, 1, new DateOnly(2026, 10, 1), "33333", "");

        var t0 = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);
        var slot = await booking.CreateSlotAsync(teacher.Id, prog.Id, "國三A班", SlotType.SmallGroup, t0.AddDays(3), t0.AddDays(3).AddHours(1), maxCapacity: 1, false, "");

        // s1 預約滿額，s2 與 s3 依序登記候補 (#1, #2)
        var b1 = await booking.CreateBookingAsync(slot.Id, s1.Id, p1User.Id, "課業提問", t0);
        var w2 = await booking.JoinWaitlistAsync(slot.Id, s2.Id, p2User.Id, "考前複習", t0.AddMinutes(1));
        var w3 = await booking.JoinWaitlistAsync(slot.Id, s3.Id, p3User.Id, "錯題訂正", t0.AddMinutes(2));

        // s1 取消預約 → 名額釋出後直接自動補上轉為正式預約並扣堂，後續候補學生自動推進順位
        await booking.CancelBookingAsync(b1.Id, p1User.Id, "改期", t0.AddMinutes(10));

        // 重新讀取驗證
        var reloadedW2 = await db.WaitlistEntries.FindAsync(w2.Id);
        var reloadedW3 = await db.WaitlistEntries.FindAsync(w3.Id);
        var reloadedS2 = await db.Students.FindAsync(s2.Id);
        var autoBookingForS2 = await db.Bookings.FirstOrDefaultAsync(b => b.TutoringSlotId == slot.Id && b.StudentId == s2.Id);

        Assert.NotNull(reloadedW2);
        Assert.Equal(WaitlistStatus.ConfirmedToBooking, reloadedW2!.Status);
        Assert.NotNull(autoBookingForS2);
        Assert.Equal(BookingStatus.Confirmed, autoBookingForS2!.Status);
        Assert.Equal(0, reloadedS2!.PaidCredits); // 自動補上直接扣抵 1 堂
        Assert.Equal(1, slot.CurrentBookedCount);

        // s3 順位推進至第 1 順位
        Assert.NotNull(reloadedW3);
        Assert.Equal(1, reloadedW3!.Position);
        Assert.Equal(WaitlistStatus.Waiting, reloadedW3.Status);
    }

    [Fact]
    public async Task ProRataSemesterBonus_SemesterExpiry_AndDropoutBatchSettlement_CalculateAccurately()
    {
        var (db, ledger, _, _) = CreateTestServices();

        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師", Role = UserRole.Teacher };
        var admin = new AppUser { Username = "a1", PasswordHash = "pw", DisplayName = "主任", Role = UserRole.Admin };
        db.Users.AddRange(teacher, admin);

        var semester = new Semester
        {
            Name = "115上",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 9), // 共 100 天
            DefaultBonusCredits = 3,
            IsCurrent = true
        };
        db.Semesters.Add(semester);

        // 插班生於第 51 天入班（剩餘 50 天，50/100 * 3 = 1.5 -> 四捨五入為 2 堂）
        var transferStudent = new Student
        {
            FullName = "插班生小華",
            GradeClass = "國二A班",
            EnrollmentDate = new DateOnly(2026, 10, 21)
        };
        db.Students.Add(transferStudent);
        await db.SaveChangesAsync();

        var bonusEntry = await ledger.GrantSemesterBonusAsync(transferStudent.Id, semester.Id, admin.Id);
        Assert.Equal(2, bonusEntry.BonusDelta);
        Assert.Equal(2, transferStudent.BonusCredits);

        // 登錄兩批不同單價的儲值：第一批 6 堂 $3,000 ($500/堂)，第二批 4 堂 $1,800 ($450/堂)
        await ledger.RecordTopUpAsync(transferStudent.Id, teacher.Id, 3000m, 6, new DateOnly(2026, 10, 21), "11111", "第一批");
        await ledger.RecordTopUpAsync(transferStudent.Id, teacher.Id, 1800m, 4, new DateOnly(2026, 11, 1), "22222", "第二批優惠");

        // 學期結束贈送堂數歸零（2 堂贈送歸零，10 堂付費保留）
        await ledger.ExpireAllBonusCreditsForSemesterEndAsync(semester.Id, admin.Id);
        Assert.Equal(0, transferStudent.BonusCredits);
        Assert.Equal(10, transferStudent.PaidCredits);

        // 消耗 4 堂付費堂數（FIFO 從第一批扣 4 堂，第一批剩 2 堂 × $500 = $1,000；第二批剩 4 堂 × $450 = $1,800，應退合計 $2,800）
        await ledger.DeductCreditsForBookingAsync(transferStudent.Id, 4, 999, teacher.Id, "老師", "測試扣抵");
        var preview = await ledger.PreviewDropoutSettlementAsync(transferStudent.Id);
        Assert.Equal(6, preview.TotalRefundablePaidCredits);
        Assert.Equal(2800m, preview.TotalRefundAmountNtd);

        var settlement = await ledger.ExecuteDropoutSettlementAsync(transferStudent.Id, admin.Id, "退班結算");
        Assert.Equal(2800m, settlement.CalculatedRefundAmountNtd);
        Assert.True(transferStudent.IsDroppedOut);
        Assert.Equal(0, transferStudent.PaidCredits);
    }

    [Fact]
    public async Task Homework48HourReminder_WorksAsExpected_AndNoShowDoesNotSuspendStudent()
    {
        var (db, _, booking, line) = CreateTestServices();

        var parent = new AppUser { Username = "p1", PasswordHash = "pw", DisplayName = "家長", Role = UserRole.Parent, LineUserId = "U_LINE_1" };
        var teacher = new AppUser { Username = "t1", PasswordHash = "pw", DisplayName = "老師", Role = UserRole.Teacher };
        var student = new Student { FullName = "學生C", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1) };
        var prog = new CourseProgress { SemesterId = 1, Code = "W05", Title = "進度5", GradeClass = "國三A班", IsCurrentProgress = true };

        db.Users.AddRange(parent, teacher);
        db.Students.Add(student);
        db.CourseProgresses.Add(prog);
        await db.SaveChangesAsync();
        db.ParentStudentLinks.Add(new ParentStudentLink { ParentUserId = parent.Id, StudentId = student.Id });
        await db.SaveChangesAsync();

        var t0 = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        // 1. 老師標記作業不佳，49 小時後仍未預約 → 應產生二次催促標示
        var hw = await line.FlagHomeworkPoorAsync(student.Id, teacher.Id, prog.Id, "作業未交", t0);
        var reminderCount = await line.Process48HourHomeworkRemindersAsync(t0.AddHours(49));
        Assert.Equal(1, reminderCount);
        Assert.True(hw.IsSecondReminderFlagged);

        // 2. 拔除爽約停權後，團體課爽約 3 次 → 不再暫停預約，堂數正常扣除/維持，學生仍可正常預約
        for (int i = 1; i <= 3; i++)
        {
            var gSlot = await booking.CreateSlotAsync(teacher.Id, prog.Id, "國三A班", SlotType.GroupCounseling, t0.AddDays(i), t0.AddDays(i).AddHours(1), 10, false, "");
            var gb = await booking.CreateBookingAsync(gSlot.Id, student.Id, parent.Id, "作業輔導", t0);
            await booking.RecordAttendanceAsync(gb.Id, AttendanceStatus.NoShow, finalizeSlot: true, t0.AddDays(i).AddHours(2));
        }

        Assert.Null(student.GroupBookingSuspendedUntilUtc);

        // 後續預約不再拋出停權例外，順利完成預約
        var nextGroupSlot = await booking.CreateSlotAsync(teacher.Id, prog.Id, "國三A班", SlotType.GroupCounseling, t0.AddDays(5), t0.AddDays(5).AddHours(1), 10, false, "");
        var nextBooking = await booking.CreateBookingAsync(nextGroupSlot.Id, student.Id, parent.Id, "作業輔導", t0.AddDays(4));
        Assert.NotNull(nextBooking);
        Assert.Equal(student.Id, nextBooking.StudentId);
    }

    [Fact]
    public async Task StudentSchoolGradeClass_SyncsAndFormatsCorrectly()
    {
        var (db, _, _, _) = CreateTestServices();

        var parent = new AppUser
        {
            Username = "parent.test",
            PasswordHash = "pw",
            DisplayName = "張媽媽（家長）",
            Role = UserRole.Parent
        };
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var student = new Student
        {
            FullName = "張小華",
            School = "敦化國中",
            Grade = "國三",
            ClassName = "A班",
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        student.SyncGradeClass();
        db.Students.Add(student);
        await db.SaveChangesAsync();

        db.ParentStudentLinks.Add(new ParentStudentLink
        {
            ParentUserId = parent.Id,
            StudentId = student.Id,
            Relationship = "母親"
        });
        await db.SaveChangesAsync();

        Assert.Equal("國三A班", student.GradeClass);
        Assert.Equal("敦化國中 · 國三A班", student.DisplaySchoolGradeClass);

        // 修改學校、年級與班級
        student.School = "介壽國中";
        student.Grade = "國二";
        student.ClassName = "805班";
        student.SyncGradeClass();
        await db.SaveChangesAsync();

        Assert.Equal("國二805班", student.GradeClass);
        Assert.Equal("介壽國中 · 國二805班", student.DisplaySchoolGradeClass);
    }

    [Fact]
    public async Task FieldOptions_SupportsNullParentValue_AndLoadsCleanly()
    {
        var (db, _, _, _) = CreateTestServices();

        // 模擬資料庫中學校、年別等非連動項目 ParentValue 為 null 的情形
        db.FieldOptions.AddRange(
            new FieldOption { Category = FieldCategories.School, Value = "三民", ParentValue = null, SortOrder = 1, IsActive = true },
            new FieldOption { Category = FieldCategories.Grade, Value = "國三", ParentValue = null, SortOrder = 1, IsActive = true },
            new FieldOption { Category = FieldCategories.Class, Value = "A班", ParentValue = "三民", SortOrder = 1, IsActive = true },
            new FieldOption { Category = FieldCategories.Subject, Value = "數學", ParentValue = null, SortOrder = 1, IsActive = true }
        );

        var teacher = new AppUser
        {
            Username = "teacher.test",
            PasswordHash = "hash",
            DisplayName = "陳老師",
            Role = UserRole.Teacher,
            Subjects = null, // 模擬既有資料欄位為 NULL
            IsProxyAdmin = true
        };
        db.Users.Add(teacher);

        var student = new Student
        {
            FullName = "測試生",
            School = null,
            Grade = null,
            ClassName = null,
            GradeClass = "國一A班"
        };
        db.Students.Add(student);

        await db.SaveChangesAsync();

        var options = await db.FieldOptions
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .ToListAsync();

        Assert.Equal(4, options.Count);
        var schoolOpt = options.First(o => o.Category == FieldCategories.School);
        Assert.Null(schoolOpt.ParentValue);

        var classOpt = options.First(o => o.Category == FieldCategories.Class);
        Assert.Equal("三民", classOpt.ParentValue);

        var fetchedTeacher = await db.Users.FindAsync(teacher.Id);
        Assert.NotNull(fetchedTeacher);
        Assert.Empty(fetchedTeacher.SubjectList);
        Assert.True(fetchedTeacher.IsProxyAdmin);

        var fetchedStudent = await db.Students.FindAsync(student.Id);
        Assert.NotNull(fetchedStudent);
        Assert.Equal("國一A班", fetchedStudent.DisplaySchoolGradeClass);
    }

    [Fact]
    public async Task SlotManagement_UpdateAndDelete_WithSafetyCheckAsync()
    {
        var (db, _, booking, _) = CreateTestServices();

        var teacher = new AppUser { Username = "teacher_slot", PasswordHash = "pw", DisplayName = "排課老師", Role = UserRole.Teacher };
        db.Users.Add(teacher);
        var progress = new CourseProgress { SemesterId = 1, Code = "M01", Title = "第一章數與式", GradeClass = "國三A班", IsCurrentProgress = true };
        db.CourseProgresses.Add(progress);
        await db.SaveChangesAsync();

        var slotDate = DateTime.UtcNow.AddDays(2);
        var slot = await booking.CreateSlotAsync(
            teacher.Id,
            progress.Id,
            "國三A班",
            SlotType.SmallGroup,
            slotDate,
            slotDate.AddMinutes(90),
            4,
            false,
            "原始備註"
        );

        Assert.Equal(4, slot.MaxCapacity);
        Assert.Equal("原始備註", slot.Notes);

        // 更新時段
        var updated = await booking.UpdateSlotAsync(
            slot.Id,
            progress.Id,
            "國三特訓班",
            SlotType.SmallGroup,
            slotDate.AddHours(1),
            slotDate.AddHours(1).AddMinutes(120),
            6,
            true,
            "更新備註：加強難題"
        );

        Assert.Equal(6, updated.MaxCapacity);
        Assert.True(updated.IsExtraSlot);
        Assert.Equal("國三特訓班", updated.GradeClass);
        Assert.Equal("更新備註：加強難題", updated.Notes);

        // 建立預約後，刪除應被阻擋
        var student = new Student { FullName = "測試學員", GradeClass = "國三A班", BonusCredits = 3 };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        await booking.CreateBookingAsync(slot.Id, student.Id, teacher.Id, "課業提問");
        await Assert.ThrowsAsync<InvalidOperationException>(() => booking.DeleteSlotAsync(slot.Id));

        // 取消預約後方可順利刪除時段
        var bk = await db.Bookings.FirstAsync(b => b.TutoringSlotId == slot.Id);
        await booking.CancelBookingAsync(bk.Id, teacher.Id, "老師調課取消", slotDate.AddDays(-1));

        await booking.DeleteSlotAsync(slot.Id);
        Assert.Null(await db.TutoringSlots.FindAsync(slot.Id));
    }

    [Fact]
    public async Task BatchRecordAttendance_UpdatesMultipleBookingsAndFinalizesSlotAsync()
    {
        var (db, _, booking, _) = CreateTestServices();

        var teacher = new AppUser { Username = "teacher_rc", PasswordHash = "pw", DisplayName = "點名老師", Role = UserRole.Teacher };
        db.Users.Add(teacher);
        var progress = new CourseProgress { SemesterId = 1, Code = "E01", Title = "英語閱讀", GradeClass = "國二A班", IsCurrentProgress = true };
        db.CourseProgresses.Add(progress);

        var s1 = new Student { FullName = "學生甲", GradeClass = "國二A班", BonusCredits = 5 };
        var s2 = new Student { FullName = "學生乙", GradeClass = "國二A班", BonusCredits = 5 };
        db.Students.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var baseTime = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
        var slotDate = baseTime.AddHours(2);
        var slot = await booking.CreateSlotAsync(
            teacher.Id,
            progress.Id,
            "國二A班",
            SlotType.SmallGroup,
            slotDate,
            slotDate.AddMinutes(90),
            4,
            false,
            "點名測試"
        );

        var b1 = await booking.CreateBookingAsync(slot.Id, s1.Id, teacher.Id, "課業提問", baseTime);
        var b2 = await booking.CreateBookingAsync(slot.Id, s2.Id, teacher.Id, "作業輔導", baseTime);

        var batchList = new List<(int BookingId, AttendanceStatus Status)>
        {
            (b1.Id, AttendanceStatus.Present),
            (b2.Id, AttendanceStatus.Absent)
        };

        await booking.BatchRecordAttendanceAsync(slot.Id, batchList, finalizeSlot: true);

        var refreshedSlot = await db.TutoringSlots.FindAsync(slot.Id);
        Assert.NotNull(refreshedSlot);
        Assert.True(refreshedSlot.IsAttendanceFinalized);

        var refreshedB1 = await db.Bookings.FindAsync(b1.Id);
        var refreshedB2 = await db.Bookings.FindAsync(b2.Id);
        Assert.Equal(AttendanceStatus.Present, refreshedB1!.AttendanceStatus);
        Assert.Equal(AttendanceStatus.Absent, refreshedB2!.AttendanceStatus);

        // 測試中文星期時間格式
        var formattedZh = BookingService.FormatTaipeiDateTimeZh(new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc));
        Assert.Contains("週", formattedZh.Replace("(", "週").Replace(")", "")); // 包含星期
        Assert.Contains("18:00", formattedZh); // UTC+8
    }

    [Fact]
    public async Task ProxyBook_WithWaiveCreditDeduction_DoesNotDeductCredits_AndResolvesHomework()
    {
        var (db, _, booking, line) = CreateTestServices();

        var teacher = new AppUser { Username = "teacher_proxy", PasswordHash = "pw", DisplayName = "陳老師", Role = UserRole.Teacher };
        var parent = new AppUser { Username = "parent_proxy", PasswordHash = "pw", DisplayName = "家長", Role = UserRole.Parent };
        var student = new Student
        {
            FullName = "王小美",
            School = "光華",
            Grade = "國二",
            ClassName = "B班",
            GradeClass = "國二B班",
            PaidCredits = 0,
            BonusCredits = 0,
            EnrollmentDate = new DateOnly(2026, 9, 1)
        };
        var prog = new CourseProgress { SemesterId = 1, Code = "W09", Title = "進度9", GradeClass = "國二B班", IsCurrentProgress = true };

        db.Users.AddRange(teacher, parent);
        db.Students.Add(student);
        db.CourseProgresses.Add(prog);
        await db.SaveChangesAsync();

        var t0 = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);

        // 1. 建立作業不佳追蹤紀錄
        var hw = await line.FlagHomeworkPoorAsync(student.Id, teacher.Id, prog.Id, "段考訂正未完成", t0);
        Assert.False(hw.IsResolvedByBooking);

        // 2. 建立小組指導時段 (原需扣 1 堂)
        var slot = await booking.CreateSlotAsync(teacher.Id, prog.Id, "國二B班", SlotType.SmallGroup, t0.AddDays(1), t0.AddDays(1).AddHours(1), 3, false, "");

        // 3. 測試家長嘗試 waiveCreditDeduction 應拋出例外
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            booking.CreateBookingAsync(slot.Id, student.Id, parent.Id, "課業提問", t0, waiveCreditDeduction: true));

        // 4. 老師使用免扣堂數代預約 (即使學生堂數為 0 亦可成功)
        var b = await booking.CreateBookingAsync(slot.Id, student.Id, teacher.Id, "課業提問", t0, waiveCreditDeduction: true);
        Assert.NotNull(b);

        // 堂數不變
        var refreshedStudent = await db.Students.FindAsync(student.Id);
        Assert.Equal(0, refreshedStudent!.PaidCredits);
        Assert.Equal(0, refreshedStudent.BonusCredits);

        // 無扣款交易流水
        var deductTx = await db.CreditLedgerEntries.FirstOrDefaultAsync(x => x.RelatedBookingId == b.Id && x.TransactionType == LedgerTransactionType.BookingDeduct);
        Assert.Null(deductTx);

        // 時段剩餘名額正常扣減
        var refreshedSlot = await db.TutoringSlots.FindAsync(slot.Id);
        Assert.Equal(2, refreshedSlot!.RemainingSeats);

        // 作業不佳追蹤自動被解決
        var refreshedHw = await db.HomeworkWatchItems.FindAsync(hw.Id);
        Assert.True(refreshedHw!.IsResolvedByBooking);
        Assert.Equal(b.Id, refreshedHw.ResolvedBookingId);

        // 5. 取消免扣堂預約時不退任何堂數
        await booking.CancelBookingAsync(b.Id, teacher.Id, "行程變更", t0.AddMinutes(5));
        var afterCancelStudent = await db.Students.FindAsync(student.Id);
        Assert.Equal(0, afterCancelStudent!.PaidCredits);
        Assert.Equal(0, afterCancelStudent.BonusCredits);
    }

    [Fact]
    public async Task EnsureCreditBatchesAndLedgerConsistent_FixesBothPaidAndBonusDiscrepancies()
    {
        var (db, ledger, _, _) = CreateTestServices();

        var admin = new AppUser { Username = "admin1", PasswordHash = "pw", DisplayName = "管理員", Role = UserRole.Admin };
        db.Users.Add(admin);

        // 模擬林小明 (主檔付費8, 贈送3; 但無儲值批次, 且流水帳贈送合計為0)
        var s1 = new Student { FullName = "林小明", GradeClass = "國三A班", EnrollmentDate = new DateOnly(2026, 9, 1), PaidCredits = 8, BonusCredits = 3 };
        db.Students.Add(s1);
        await db.SaveChangesAsync();

        // 建立初始流水帳僅有付費8堂，贈送為0 (模擬資料不一致情境)
        db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            StudentId = s1.Id,
            TransactionType = LedgerTransactionType.TopUpPaid,
            PaidDelta = 8,
            BonusDelta = 0,
            PaidBalanceAfter = 8,
            BonusBalanceAfter = 3,
            ReferenceNote = "初始付費",
            OperatorName = "測試",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // 執行對帳應發現異常 (缺少儲值批次8堂，缺少贈送流水帳3堂)
        var discrepanciesBefore = await ledger.RunNightlyReconciliationAsync();
        Assert.NotEmpty(discrepanciesBefore);
        Assert.Contains(discrepanciesBefore, d => d.StudentId == s1.Id);

        // 執行一鍵自動同步校準
        await DbInitializer.EnsureCreditBatchesAndLedgerConsistentAsync(db);

        // 重新對帳應 100% 吻合 (0 筆異常)
        var discrepanciesAfter = await ledger.RunNightlyReconciliationAsync();
        Assert.Empty(discrepanciesAfter);
    }
}



