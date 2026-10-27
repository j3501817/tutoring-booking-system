using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly CreditLedgerService _ledgerService;
    private readonly LineNotificationService _lineService;

    public BookingService(
        AppDbContext db,
        CreditLedgerService ledgerService,
        LineNotificationService lineService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _lineService = lineService;
    }

    public static DateTime ToTaipeiTime(DateTime utcTime)
    {
        return utcTime.AddHours(8);
    }

    public static string FormatTaipeiDateTimeZh(DateTime utcTime)
    {
        var tpe = ToTaipeiTime(utcTime);
        var day = "日一二三四五六"[(int)tpe.DayOfWeek];
        return $"{tpe:yyyy/MM/dd} ({day}) {tpe:HH:mm}";
    }

    public static string FormatTaipeiShortDateZh(DateTime utcTime)
    {
        var tpe = ToTaipeiTime(utcTime);
        var day = "日一二三四五六"[(int)tpe.DayOfWeek];
        return $"{tpe:MM/dd} ({day}) {tpe:HH:mm}";
    }

    public async Task<TutoringSlot> CreateSlotAsync(
        int teacherUserId,
        int courseProgressId,
        string gradeClass,
        SlotType slotType,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int maxCapacity,
        bool isExtraSlot,
        string notes)
    {
        if (maxCapacity <= 0)
            throw new InvalidOperationException("時段容納名額必須大於 0。");
        if (endsAtUtc <= startsAtUtc)
            throw new InvalidOperationException("結束時間必須晚於開始時間。");

        var teacher = await _db.Users.FindAsync(teacherUserId)
            ?? throw new InvalidOperationException("找不到老師帳號。");
        if (teacher.Role != UserRole.Teacher && teacher.Role != UserRole.Admin)
            throw new InvalidOperationException("僅老師或管理員可建立輔導時段。");

        var progress = await _db.CourseProgresses.FindAsync(courseProgressId)
            ?? throw new InvalidOperationException("找不到指定的課程進度。");

        var slot = new TutoringSlot
        {
            TeacherUserId = teacherUserId,
            CourseProgressId = courseProgressId,
            GradeClass = string.IsNullOrWhiteSpace(gradeClass) ? progress.GradeClass : gradeClass.Trim(),
            SlotType = slotType,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            MaxCapacity = maxCapacity,
            CurrentBookedCount = 0,
            IsExtraSlot = isExtraSlot,
            Notes = notes.Trim()
        };

        _db.TutoringSlots.Add(slot);
        await _db.SaveChangesAsync();
        return slot;
    }

    public async Task<TutoringSlot> UpdateSlotAsync(
        int slotId,
        int courseProgressId,
        string gradeClass,
        SlotType slotType,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int maxCapacity,
        bool isExtraSlot,
        string notes)
    {
        if (maxCapacity <= 0)
            throw new InvalidOperationException("時段容納名額必須大於 0。");
        if (endsAtUtc <= startsAtUtc)
            throw new InvalidOperationException("結束時間必須晚於開始時間。");

        var slot = await _db.TutoringSlots.FindAsync(slotId)
            ?? throw new InvalidOperationException("找不到指定的輔導時段。");

        if (maxCapacity < slot.CurrentBookedCount)
            throw new InvalidOperationException($"容納名額不可小於已預約人數 ({slot.CurrentBookedCount} 人)。");

        var progress = await _db.CourseProgresses.FindAsync(courseProgressId)
            ?? throw new InvalidOperationException("找不到指定的課程進度。");

        slot.CourseProgressId = courseProgressId;
        slot.GradeClass = string.IsNullOrWhiteSpace(gradeClass) ? progress.GradeClass : gradeClass.Trim();
        slot.SlotType = slotType;
        slot.StartsAtUtc = startsAtUtc;
        slot.EndsAtUtc = endsAtUtc;
        slot.MaxCapacity = maxCapacity;
        slot.IsExtraSlot = isExtraSlot;
        slot.Notes = notes?.Trim() ?? string.Empty;

        await _db.SaveChangesAsync();
        return slot;
    }

    public async Task DeleteSlotAsync(int slotId)
    {
        var slot = await _db.TutoringSlots.FindAsync(slotId)
            ?? throw new InvalidOperationException("找不到指定的輔導時段。");

        var hasActiveBookings = await _db.Bookings.AnyAsync(b => b.TutoringSlotId == slotId && b.Status == BookingStatus.Confirmed);
        if (hasActiveBookings)
        {
            throw new InvalidOperationException("此時段已有學生預約，無法直接刪除；請先取消該時段預約或進行改期。");
        }

        var waitlist = await _db.WaitlistEntries.Where(w => w.TutoringSlotId == slotId).ToListAsync();
        if (waitlist.Any())
        {
            _db.WaitlistEntries.RemoveRange(waitlist);
        }
        var inactiveBookings = await _db.Bookings.Where(b => b.TutoringSlotId == slotId).ToListAsync();
        if (inactiveBookings.Any())
        {
            _db.Bookings.RemoveRange(inactiveBookings);
        }

        _db.TutoringSlots.Remove(slot);
        await _db.SaveChangesAsync();
    }



    public async Task<Booking> CreateBookingAsync(
        int slotId,
        int studentId,
        int operatorUserId,
        string purposeName,
        DateTime? nowUtc = null,
        bool waiveCreditDeduction = false)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        await ProcessExpiredWaitlistHoldsAsync(timestamp);

        var slot = await _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new InvalidOperationException("找不到指定的輔導時段。");

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");

        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人帳號。");

        if (student.IsDroppedOut || student.EnrollmentStatus != StudentEnrollmentStatus.Enrolled)
            throw new InvalidOperationException("該學生本學期尚未完成續報（或已退班），不可預約課堂；請先於學生管理辦理續報。");

        if (op.Role == UserRole.Parent)
        {
            var isLinked = await _db.ParentStudentLinks
                .AnyAsync(l => l.ParentUserId == op.Id && l.StudentId == studentId);
            if (!isLinked)
                throw new InvalidOperationException("您無權限替非關聯的孩子預約。");
        }

        var validPurpose = await _db.AttendancePurposeOptions
            .AnyAsync(p => p.IsActive && p.Name == purposeName.Trim());
        if (!validPurpose)
            throw new InvalidOperationException("請選擇有效的到班目的。");

        if (slot.CourseProgress == null || !slot.CourseProgress.IsCurrentProgress)
            throw new InvalidOperationException("依規定僅可預約「目前開放進度」之時段。");

        var slotDate = DateOnly.FromDateTime(ToTaipeiTime(slot.StartsAtUtc));
        var isHoliday = await _db.AcademicHolidays.AnyAsync(h => h.IsActive && h.Date == slotDate);
        if (isHoliday)
            throw new InvalidOperationException("該日期為補習班休假日，不開放預約。");

        if (slot.StartsAtUtc <= timestamp)
            throw new InvalidOperationException("該時段已開課或結束，無法預約。");

        if ((slot.StartsAtUtc - timestamp).TotalMinutes < 30.0)
            throw new InvalidOperationException("開課前 30 分鐘截止預約，目前無法進行預約。");

        if (slot.RemainingSeats <= 0)
            throw new InvalidOperationException("該時段名額已滿，請改為登記候補。");

        var duplicateInSameSlot = await _db.Bookings
            .AnyAsync(b => b.TutoringSlotId == slotId
                           && b.StudentId == studentId
                           && b.Status == BookingStatus.Confirmed);
        if (duplicateInSameSlot)
            throw new InvalidOperationException("該學生已預約此時段，請勿重複預約。");

        if (slot.SlotType == SlotType.SmallGroup)
        {
            var alreadyHasSmallGroupInProgress = await _db.Bookings
                .Include(b => b.TutoringSlot)
                .AnyAsync(b => b.StudentId == studentId
                               && b.Status == BookingStatus.Confirmed
                               && b.TutoringSlot != null
                               && b.TutoringSlot.CourseProgressId == slot.CourseProgressId
                               && b.TutoringSlot.SlotType == SlotType.SmallGroup);

            if (alreadyHasSmallGroupInProgress)
                throw new InvalidOperationException("小組指導每個課程進度限預約 1 堂（若需更換時間請使用「同進度改期」功能）。");
        }

        // 僅老師與管理員具備免扣堂權限，若非老師/管理員則不允許免扣堂
        var isTeacherOrAdmin = op.Role == UserRole.Teacher || op.Role == UserRole.Admin;
        var isFreeWaived = waiveCreditDeduction && isTeacherOrAdmin;

        var creditsNeeded = (slot.SlotType == SlotType.SmallGroup && !isFreeWaived) ? 1 : 0;
        if (creditsNeeded > 0 && student.TotalCredits < creditsNeeded)
        {
            throw new InvalidOperationException($"學生堂數餘額不足（目前付費 {student.PaidCredits} 堂 + 贈送 {student.BonusCredits} 堂，小組指導需 1 堂）。");
        }


        var booking = new Booking
        {
            TutoringSlotId = slot.Id,
            StudentId = student.Id,
            CreatedByUserId = op.Id,
            CreatedByName = op.DisplayName,
            CreatedByRole = op.Role,
            PurposeName = purposeName.Trim(),
            Status = BookingStatus.Confirmed,
            CreditsDeducted = creditsNeeded,
            BonusCreditsUsed = 0,
            PaidCreditsUsed = 0,
            AttendanceStatus = AttendanceStatus.Pending,
            CreatedAtUtc = timestamp
        };

        slot.CurrentBookedCount += 1;
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        if (creditsNeeded > 0)
        {
            var tpeStart = ToTaipeiTime(slot.StartsAtUtc);
            var summary = $"{slot.CourseProgress.Code} 小組指導 {tpeStart:MM/dd HH:mm}";
            var (bonusUsed, paidUsed) = await _ledgerService.DeductCreditsForBookingAsync(
                student.Id,
                creditsNeeded,
                booking.Id,
                op.Id,
                op.DisplayName,
                summary,
                timestamp);

            booking.BonusCreditsUsed = bonusUsed;
            booking.PaidCreditsUsed = paidUsed;
        }

        var openHomeworkItems = await _db.HomeworkWatchItems
            .Where(h => h.StudentId == student.Id && !h.IsResolvedByBooking)
            .ToListAsync();
        foreach (var hw in openHomeworkItems)
        {
            hw.IsResolvedByBooking = true;
            hw.ResolvedBookingId = booking.Id;
        }

        await _db.SaveChangesAsync();

        try
        {
            var tpeTime = FormatTaipeiDateTimeZh(slot.StartsAtUtc);
            var progTitle = $"{slot.CourseProgress?.Code} {slot.CourseProgress?.Title}".Trim();
            await _lineService.NotifyBookingCompletedAsync(student.Id, tpeTime, progTitle, booking.PurposeName, op.DisplayName, timestamp);
        }
        catch { }

        return booking;
    }

    public async Task<(Booking CancelledBooking, bool Refunded)> CancelBookingAsync(
        int bookingId,
        int operatorUserId,
        string reason,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var booking = await _db.Bookings
            .Include(b => b.TutoringSlot)
            .ThenInclude(s => s!.CourseProgress)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new InvalidOperationException("找不到預約紀錄。");

        if (booking.Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("此預約並非有效成立狀態，無法取消。");

        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人帳號。");

        if (op.Role == UserRole.Parent)
        {
            var isLinked = await _db.ParentStudentLinks
                .AnyAsync(l => l.ParentUserId == op.Id && l.StudentId == booking.StudentId);
            if (!isLinked)
                throw new InvalidOperationException("您無權限取消此預約。");
        }

        var slot = booking.TutoringSlot!;
        var hoursBeforeStart = (slot.StartsAtUtc - timestamp).TotalHours;
        var canRefund = hoursBeforeStart >= 24.0;

        booking.CancelledAtUtc = timestamp;
        booking.CancelReason = string.IsNullOrWhiteSpace(reason) ? "使用者取消" : reason.Trim();
        booking.Status = canRefund ? BookingStatus.CancelledOnTime : BookingStatus.CancelledLate;

        if (slot.CurrentBookedCount > 0)
        {
            slot.CurrentBookedCount -= 1;
        }

        if (canRefund && booking.CreditsDeducted > 0)
        {
            var tpeStart = ToTaipeiTime(slot.StartsAtUtc);
            var summary = $"{slot.CourseProgress?.Code} {tpeStart:MM/dd HH:mm}";
            await _ledgerService.RefundCreditsForCancellationAsync(
                booking.StudentId,
                booking.BonusCreditsUsed,
                booking.PaidCreditsUsed,
                booking.Id,
                op.Id,
                op.DisplayName,
                summary,
                timestamp);
        }

        await _db.SaveChangesAsync();

        try
        {
            if (canRefund && booking.CreditsDeducted > 0)
            {
                var s = await _db.Students.FindAsync(booking.StudentId);
                var tpeTime = FormatTaipeiDateTimeZh(slot.StartsAtUtc);
                var progTitle = $"{slot.CourseProgress?.Code} {slot.CourseProgress?.Title}".Trim();
                if (s != null)
                {
                    await _lineService.NotifyCreditsChangedAsync(
                        booking.StudentId,
                        "取消預約退還",
                        $"取消預約 {progTitle}（{tpeTime}）退還堂數",
                        booking.PaidCreditsUsed,
                        booking.BonusCreditsUsed,
                        s.PaidCredits,
                        s.BonusCredits,
                        timestamp);
                }
            }
        }
        catch { }

        await PromoteNextWaitlistEntryAsync(slot.Id, timestamp);

        return (booking, canRefund);
    }

    public async Task<Booking> RescheduleBookingAsync(
        int existingBookingId,
        int targetSlotId,
        int operatorUserId,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var existingBooking = await _db.Bookings
            .Include(b => b.TutoringSlot)
            .FirstOrDefaultAsync(b => b.Id == existingBookingId)
            ?? throw new InvalidOperationException("找不到原預約紀錄。");

        if (existingBooking.Status != BookingStatus.Confirmed)
            throw new InvalidOperationException("原預約非有效狀態，無法改期。");

        var targetSlot = await _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .FirstOrDefaultAsync(s => s.Id == targetSlotId)
            ?? throw new InvalidOperationException("找不到目標時段。");

        if (existingBooking.TutoringSlot!.CourseProgressId != targetSlot.CourseProgressId)
            throw new InvalidOperationException("依規定僅限於「相同課程進度」內改期。");

        if ((existingBooking.TutoringSlot.StartsAtUtc - timestamp).TotalHours < 24.0)
            throw new InvalidOperationException("距離原時段開課已不足 24 小時，無法改期。");

        if (targetSlot.RemainingSeats <= 0)
            throw new InvalidOperationException("目標時段名額已滿，無法改期。");

        var oldSlotId = existingBooking.TutoringSlotId;
        var studentId = existingBooking.StudentId;
        var purposeName = existingBooking.PurposeName;

        await CancelBookingAsync(existingBookingId, operatorUserId, $"同進度改期至時段 #{targetSlotId}", timestamp);
        existingBooking.Status = BookingStatus.Rescheduled;
        await _db.SaveChangesAsync();

        return await CreateBookingAsync(targetSlotId, studentId, operatorUserId, purposeName, timestamp);
    }

    public async Task<WaitlistEntry> JoinWaitlistAsync(
        int slotId,
        int studentId,
        int parentUserId,
        string purposeName,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        await ProcessExpiredWaitlistHoldsAsync(timestamp);

        var slot = await _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new InvalidOperationException("找不到指定的輔導時段。");

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        if (student.IsDroppedOut || student.EnrollmentStatus != StudentEnrollmentStatus.Enrolled)
            throw new InvalidOperationException("該學生本學期尚未完成續報（或已退班），不可登記候補；請先於學生管理辦理續報。");

        if (slot.RemainingSeats > 0)
            throw new InvalidOperationException("此時段尚有空位，請直接預約無須候補。");

        var alreadyWaitlisted = await _db.WaitlistEntries
            .AnyAsync(w => w.TutoringSlotId == slotId
                           && w.StudentId == studentId
                           && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.NotifiedHold));
        if (alreadyWaitlisted)
            throw new InvalidOperationException("該學生已在候補名單中。");

        var maxPos = await _db.WaitlistEntries
            .Where(w => w.TutoringSlotId == slotId)
            .Select(w => (int?)w.Position)
            .MaxAsync() ?? 0;

        var entry = new WaitlistEntry
        {
            TutoringSlotId = slotId,
            StudentId = studentId,
            ParentUserId = parentUserId,
            PurposeName = purposeName.Trim(),
            Position = maxPos + 1,
            Status = WaitlistStatus.Waiting,
            CreatedAtUtc = timestamp
        };

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<WaitlistEntry?> PromoteNextWaitlistEntryAsync(int slotId, DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var slot = await _db.TutoringSlots
            .Include(s => s.CourseProgress)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null || slot.RemainingSeats <= 0 || slot.StartsAtUtc <= timestamp)
            return null;

        while (slot.RemainingSeats > 0)
        {
            var nextWaiting = await _db.WaitlistEntries
                .Include(w => w.Student)
                .Where(w => w.TutoringSlotId == slotId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.Position)
                .FirstOrDefaultAsync();

            if (nextWaiting == null)
                return null;

            var student = nextWaiting.Student;
            if (student == null || student.IsDroppedOut)
            {
                nextWaiting.Status = WaitlistStatus.Expired;
                await _db.SaveChangesAsync();
                continue;
            }

            // 檢查重複預約（同一進度小組指導限 1 堂）
            var hasConflict = false;
            if (slot.SlotType == SlotType.SmallGroup)
            {
                var hasOtherSmallGroup = await _db.Bookings
                    .Include(b => b.TutoringSlot)
                    .AnyAsync(b => b.StudentId == student.Id
                                   && b.Status == BookingStatus.Confirmed
                                   && b.TutoringSlot != null
                                   && b.TutoringSlot.CourseProgressId == slot.CourseProgressId
                                   && b.TutoringSlot.SlotType == SlotType.SmallGroup);
                if (hasOtherSmallGroup)
                {
                    hasConflict = true;
                }
            }

            var creditsNeeded = slot.SlotType == SlotType.SmallGroup ? 1 : 0;
            if (hasConflict || (creditsNeeded > 0 && student.TotalCredits < creditsNeeded))
            {
                // 堂數不足或已有相同進度預約，將其標為失效並遞補下一位
                nextWaiting.Status = WaitlistStatus.Expired;
                nextWaiting.CancelReason = hasConflict ? "該進度已有小組指導預約，無法重複遞補" : "剩餘堂數不足，無法自動扣堂遞補";
                await _db.SaveChangesAsync();
                continue;
            }

            // 直接轉為正式預約並扣堂
            nextWaiting.Status = WaitlistStatus.ConfirmedToBooking;
            nextWaiting.ConfirmedAtUtc = timestamp;
            slot.CurrentBookedCount += 1;

            var booking = new Booking
            {
                TutoringSlotId = slot.Id,
                StudentId = student.Id,
                CreatedByUserId = nextWaiting.ParentUserId,
                CreatedByName = student.FullName + " 家長(候補自動轉正)",
                CreatedByRole = UserRole.Parent,
                PurposeName = nextWaiting.PurposeName,
                Status = BookingStatus.Confirmed,
                CreditsDeducted = creditsNeeded,
                AttendanceStatus = AttendanceStatus.Pending,
                CreatedAtUtc = timestamp
            };
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            if (creditsNeeded > 0)
            {
                var tpeS = ToTaipeiTime(slot.StartsAtUtc);
                var summary = $"候補自動轉正 {slot.CourseProgress?.Code} {tpeS:MM/dd HH:mm}";
                var (bonusUsed, paidUsed) = await _ledgerService.DeductCreditsForBookingAsync(
                    student.Id,
                    creditsNeeded,
                    booking.Id,
                    nextWaiting.ParentUserId,
                    "系統候補自動轉正",
                    summary,
                    timestamp);

                booking.BonusCreditsUsed = bonusUsed;
                booking.PaidCreditsUsed = paidUsed;
                await _db.SaveChangesAsync();
            }

            // 同時段其餘候補學生順位往前遞補
            var subsequent = await _db.WaitlistEntries
                .Where(w => w.TutoringSlotId == slotId && w.Status == WaitlistStatus.Waiting && w.Position > nextWaiting.Position)
                .OrderBy(w => w.Position)
                .ToListAsync();
            foreach (var w in subsequent)
            {
                w.Position = Math.Max(1, w.Position - 1);
            }
            await _db.SaveChangesAsync();

            // 發送 LINE 候補直接轉正通知
            try
            {
                var tpeStart = ToTaipeiTime(slot.StartsAtUtc);
                var tpeTime = FormatTaipeiDateTimeZh(slot.StartsAtUtc);
                var progTitle = $"{slot.CourseProgress?.Code} {slot.CourseProgress?.Title}".Trim();
                await _lineService.SendToStudentParentsAsync(
                    student.Id,
                    "WaitlistPromotedAuto",
                    $"waitlist-auto-promote:{nextWaiting.Id}:{timestamp.Ticks}",
                    $"【候補成功轉正通知】{student.FullName} 候補之「{progTitle} ({tpeStart:MM/dd HH:mm})」已成功自動遞補為正式預約！已扣抵 {creditsNeeded} 堂課，請準時出席。",
                    $"/Parent?studentId={student.Id}",
                    timestamp);
            }
            catch { }

            return nextWaiting;
        }

        return null;
    }

    public async Task<Booking> ConfirmWaitlistHoldAsync(
        int waitlistEntryId,
        int operatorUserId,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var entry = await _db.WaitlistEntries
            .Include(w => w.TutoringSlot)
            .ThenInclude(s => s!.CourseProgress)
            .Include(w => w.Student)
            .FirstOrDefaultAsync(w => w.Id == waitlistEntryId)
            ?? throw new InvalidOperationException("找不到候補紀錄。");

        if (entry.Status != WaitlistStatus.NotifiedHold)
            throw new InvalidOperationException("此候補紀錄目前不在「可確認保留」狀態。");

        if (entry.HoldExpiresAtUtc.HasValue && entry.HoldExpiresAtUtc.Value < timestamp)
        {
            await ProcessExpiredWaitlistHoldsAsync(timestamp);
            throw new InvalidOperationException("候補保留 30 分鐘已逾時，系統已自動輪替給下一順位。");
        }

        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var slot = entry.TutoringSlot!;
        var student = entry.Student!;

        if (slot.SlotType == SlotType.SmallGroup)
        {
            var hasOtherSmallGroup = await _db.Bookings
                .Include(b => b.TutoringSlot)
                .AnyAsync(b => b.StudentId == student.Id
                               && b.Status == BookingStatus.Confirmed
                               && b.TutoringSlot != null
                               && b.TutoringSlot.CourseProgressId == slot.CourseProgressId
                               && b.TutoringSlot.SlotType == SlotType.SmallGroup);
            if (hasOtherSmallGroup)
                throw new InvalidOperationException("該進度已有另一筆有效的小組指導預約，每進度限 1 堂。");
        }

        var creditsNeeded = slot.SlotType == SlotType.SmallGroup ? 1 : 0;
        if (creditsNeeded > 0 && student.TotalCredits < creditsNeeded)
            throw new InvalidOperationException("學生堂數不足，請先由老師儲值後再確認候補。");

        entry.Status = WaitlistStatus.ConfirmedToBooking;
        entry.ConfirmedAtUtc = timestamp;

        var booking = new Booking
        {
            TutoringSlotId = slot.Id,
            StudentId = student.Id,
            CreatedByUserId = op.Id,
            CreatedByName = op.DisplayName,
            CreatedByRole = op.Role,
            PurposeName = entry.PurposeName,
            Status = BookingStatus.Confirmed,
            CreditsDeducted = creditsNeeded,
            AttendanceStatus = AttendanceStatus.Pending,
            CreatedAtUtc = timestamp
        };

        // 名額在進入 NotifiedHold 時已先保留 +1，此處直接轉為正式預約不重複 +1
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        if (creditsNeeded > 0)
        {
            var tpeStart = ToTaipeiTime(slot.StartsAtUtc);
            var summary = $"候補轉正 {slot.CourseProgress?.Code} {tpeStart:MM/dd HH:mm}";
            var (bonusUsed, paidUsed) = await _ledgerService.DeductCreditsForBookingAsync(
                student.Id,
                creditsNeeded,
                booking.Id,
                op.Id,
                op.DisplayName,
                summary,
                timestamp);

            booking.BonusCreditsUsed = bonusUsed;
            booking.PaidCreditsUsed = paidUsed;
            await _db.SaveChangesAsync();
        }

        try
        {
            var tpeTime = FormatTaipeiDateTimeZh(slot.StartsAtUtc);
            var progTitle = $"{slot.CourseProgress?.Code} {slot.CourseProgress?.Title}".Trim();
            await _lineService.NotifyBookingCompletedAsync(student.Id, tpeTime, progTitle, booking.PurposeName, op.DisplayName, timestamp);
        }
        catch { }

        return booking;
    }

    public async Task<WaitlistEntry> CancelWaitlistAsync(
        int waitlistEntryId,
        int operatorUserId,
        string? reason = null,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var entry = await _db.WaitlistEntries
            .Include(w => w.TutoringSlot)
            .ThenInclude(s => s!.CourseProgress)
            .Include(w => w.Student)
            .FirstOrDefaultAsync(w => w.Id == waitlistEntryId)
            ?? throw new InvalidOperationException("找不到指定的候補紀錄。");

        if (entry.Status != WaitlistStatus.Waiting && entry.Status != WaitlistStatus.NotifiedHold)
            throw new InvalidOperationException("此候補紀錄非等待中或保留中狀態，無法取消。");

        var wasHold = entry.Status == WaitlistStatus.NotifiedHold;
        var cancelledPos = entry.Position;
        var slotId = entry.TutoringSlotId;
        var slot = entry.TutoringSlot;

        entry.Status = WaitlistStatus.Cancelled;
        entry.CancelledAtUtc = timestamp;
        entry.CancelReason = string.IsNullOrWhiteSpace(reason) ? "家長自行取消候補" : reason.Trim();

        // 若原本處於 NotifiedHold，需釋放時段鎖定名額
        if (wasHold && slot != null)
        {
            slot.CurrentBookedCount = Math.Max(0, slot.CurrentBookedCount - 1);
        }

        // 同時段後續 Waiting 候補學生，順位向前遞補（Position - 1）
        var subsequentWaiting = await _db.WaitlistEntries
            .Where(w => w.TutoringSlotId == slotId 
                         && w.Status == WaitlistStatus.Waiting 
                         && w.Position > cancelledPos)
            .OrderBy(w => w.Position)
            .ToListAsync();

        foreach (var w in subsequentWaiting)
        {
            w.Position -= 1;
        }

        // 記錄歷程到 LineNotificationLog
        var tpeStart = slot != null ? ToTaipeiTime(slot.StartsAtUtc) : timestamp;
        var progCode = slot?.CourseProgress?.Code ?? "";
        var logMsg = $"【候補取消歷程】學生「{entry.Student?.FullName}」已取消 {tpeStart:yyyy/MM/dd HH:mm} ({progCode}) 之候補（原第 #{cancelledPos} 順位），其餘後續候補學生已自動順位遞補。";

        _db.LineNotificationLogs.Add(new LineNotificationLog
        {
            RecipientParentUserId = entry.ParentUserId,
            NotificationType = "WaitlistCancelled",
            IdempotencyKey = $"waitlist-cancel-{entry.Id}-{timestamp.Ticks}",
            MessageText = logMsg,
            Status = "Recorded",
            SentAtUtc = timestamp
        });

        await _db.SaveChangesAsync();

        // 若剛好釋出了保留名額，自動為下一位遞補發出保留通知
        if (wasHold && slot != null)
        {
            await PromoteNextWaitlistEntryAsync(slot.Id, timestamp);
        }

        return entry;
    }

    public async Task<int> ProcessExpiredWaitlistHoldsAsync(DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var expiredHolds = await _db.WaitlistEntries
            .Include(w => w.TutoringSlot)
            .Where(w => w.Status == WaitlistStatus.NotifiedHold
                        && w.HoldExpiresAtUtc.HasValue
                        && w.HoldExpiresAtUtc.Value < timestamp)
            .ToListAsync();

        var rotatedCount = 0;
        foreach (var hold in expiredHolds)
        {
            hold.Status = WaitlistStatus.Expired;
            if (hold.TutoringSlot != null && hold.TutoringSlot.CurrentBookedCount > 0)
            {
                hold.TutoringSlot.CurrentBookedCount -= 1;
            }
            rotatedCount++;
            await _db.SaveChangesAsync();

            if (hold.TutoringSlot != null)
            {
                await PromoteNextWaitlistEntryAsync(hold.TutoringSlot.Id, timestamp);
            }
        }

        return rotatedCount;
    }

    public async Task RecordAttendanceAsync(
        int bookingId,
        AttendanceStatus attendanceStatus,
        bool finalizeSlot,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var booking = await _db.Bookings
            .Include(b => b.TutoringSlot)
            .Include(b => b.Student)
            .FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new InvalidOperationException("找不到預約紀錄。");

        booking.AttendanceStatus = attendanceStatus;
        if (finalizeSlot && booking.TutoringSlot != null)
        {
            booking.TutoringSlot.IsAttendanceFinalized = true;
        }

        await _db.SaveChangesAsync();
    }

    public async Task BatchRecordAttendanceAsync(
        int slotId,
        List<(int BookingId, AttendanceStatus Status)> items,
        bool finalizeSlot,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var slot = await _db.TutoringSlots.FindAsync(slotId)
            ?? throw new InvalidOperationException("找不到指定的輔導時段。");

        foreach (var item in items)
        {
            await RecordAttendanceAsync(item.BookingId, item.Status, false, timestamp);
        }

        if (finalizeSlot)
        {
            slot.IsAttendanceFinalized = true;
            await _db.SaveChangesAsync();
        }
    }
}

