using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class LineNotificationService
{
    private readonly AppDbContext _db;
    private readonly LineAuthService? _lineAuth;

    public LineNotificationService(AppDbContext db, LineAuthService? lineAuth = null)
    {
        _db = db;
        _lineAuth = lineAuth;
    }

    public async Task<LineNotificationLog?> SendToStudentParentsAsync(
        int studentId,
        string notificationType,
        string idempotencyKeyPrefix,
        string messageText,
        string actionUrl,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var links = await _db.ParentStudentLinks
            .Include(l => l.ParentUser)
            .Where(l => l.StudentId == studentId)
            .ToListAsync();

        LineNotificationLog? firstLog = null;
        foreach (var link in links)
        {
            if (link.ParentUser == null) continue;
            var key = $"{idempotencyKeyPrefix}:parent-{link.ParentUserId}";
            var existing = await _db.LineNotificationLogs
                .FirstOrDefaultAsync(l => l.IdempotencyKey == key);
            if (existing != null)
            {
                firstLog ??= existing;
                continue;
            }

            var status = string.IsNullOrWhiteSpace(link.ParentUser.LineUserId) ? "Failed_NoLineId" : "Sent";
            if (_lineAuth != null && !string.IsNullOrWhiteSpace(link.ParentUser.LineUserId))
            {
                var (pSuccess, pMsg) = await _lineAuth.SendPushMessageAsync(link.ParentUser.LineUserId, messageText);
                if (!pSuccess && !pMsg.Contains("尚未設定"))
                {
                    status = "Failed_LineApi";
                }
            }

            var log = new LineNotificationLog
            {
                RecipientParentUserId = link.ParentUserId,
                LineUserId = link.ParentUser.LineUserId ?? "UNBOUND",
                NotificationType = notificationType,
                IdempotencyKey = key,
                MessageText = messageText,
                ActionUrl = actionUrl,
                Status = status,
                RetryCount = 0,
                SentAtUtc = timestamp
            };
            _db.LineNotificationLogs.Add(log);
            firstLog ??= log;
        }

        await _db.SaveChangesAsync();
        return firstLog;
    }

    public async Task<HomeworkWatchItem> FlagHomeworkPoorAsync(
        int studentId,
        int teacherUserId,
        int courseProgressId,
        string subjectNote,
        DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        var progress = await _db.CourseProgresses.FindAsync(courseProgressId)
            ?? throw new InvalidOperationException("找不到課程進度。");

        var item = new HomeworkWatchItem
        {
            StudentId = studentId,
            TeacherUserId = teacherUserId,
            CourseProgressId = courseProgressId,
            SubjectNote = subjectNote,
            FirstNotifiedAtUtc = timestamp,
            CreatedAtUtc = timestamp
        };
        _db.HomeworkWatchItems.Add(item);
        await _db.SaveChangesAsync();

        await SendToStudentParentsAsync(
            studentId,
            "HomeworkPoor",
            $"hw-poor:{item.Id}",
            $"【柏拉圖補習班通知】{student.FullName} 於進度「{progress.Code} {progress.Title}」作業狀況需加強（{subjectNote}），請前往網站預約輔導時段。",
            $"/Parent?studentId={studentId}",
            timestamp);

        return item;
    }

    public async Task<int> Process48HourHomeworkRemindersAsync(DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var cutoff = timestamp.AddHours(-48);

        var overdueItems = await _db.HomeworkWatchItems
            .Include(h => h.Student)
            .Include(h => h.CourseProgress)
            .Where(h => !h.IsResolvedByBooking
                        && !h.IsSecondReminderFlagged
                        && h.FirstNotifiedAtUtc <= cutoff)
            .ToListAsync();

        var count = 0;
        foreach (var item in overdueItems)
        {
            var hasBookedSince = await _db.Bookings
                .Include(b => b.TutoringSlot)
                .AnyAsync(b => b.StudentId == item.StudentId
                               && b.Status == BookingStatus.Confirmed
                               && b.CreatedAtUtc >= item.FirstNotifiedAtUtc);

            if (hasBookedSince)
            {
                item.IsResolvedByBooking = true;
                continue;
            }

            item.IsSecondReminderFlagged = true;
            item.SecondReminderSentAtUtc = timestamp;
            count++;

            await SendToStudentParentsAsync(
                item.StudentId,
                "HomeworkReminder48h",
                $"hw-48h:{item.Id}",
                $"【二次提醒】{item.Student?.FullName} 之作業輔導通知已逾 48 小時尚未完成預約（{item.SubjectNote}），請儘速登入網站完成預約。",
                $"/Parent?studentId={item.StudentId}",
                timestamp);
        }

        await _db.SaveChangesAsync();
        return count;
    }

    public async Task<bool> RetryFailedNotificationAsync(int logId, DateTime? nowUtc = null)
    {
        var log = await _db.LineNotificationLogs
            .Include(l => l.RecipientParentUser)
            .FirstOrDefaultAsync(l => l.Id == logId);
        if (log == null) return false;

        log.RetryCount++;
        log.SentAtUtc = nowUtc ?? DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(log.RecipientParentUser?.LineUserId))
        {
            log.LineUserId = log.RecipientParentUser.LineUserId;
            if (_lineAuth != null)
            {
                var (pSuccess, pMsg) = await _lineAuth.SendPushMessageAsync(log.LineUserId, log.MessageText);
                log.Status = (pSuccess || pMsg.Contains("尚未設定")) ? "RetriedSuccess" : "Failed_LineApi";
            }
            else
            {
                log.Status = "RetriedSuccess";
            }
        }
        else
        {
            log.Status = "Failed_NoLineId";
        }

        await _db.SaveChangesAsync();
        return log.Status == "RetriedSuccess";
    }

    /// <summary>1. 堂數有異動時通知（獲得贈券、完成預約扣堂、取消預約退還、退還費用）</summary>
    public async Task NotifyCreditsChangedAsync(int studentId, string changeTypeZh, string note, int paidDelta, int bonusDelta, int paidAfter, int bonusAfter, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;
        var deltaText = "";
        if (bonusDelta != 0) deltaText += $"贈送 {(bonusDelta > 0 ? "+" : "")}{bonusDelta} 堂 ";
        if (paidDelta != 0) deltaText += $"付費 {(paidDelta > 0 ? "+" : "")}{paidDelta} 堂 ";

        var msg = $"【柏拉圖補習班・堂數異動】{student.FullName} 之輔導堂數有異動（{changeTypeZh}）。\n" +
                  $"異動內容：{deltaText.Trim()}\n" +
                  $"目前餘額：付費 {paidAfter} 堂 / 贈送 {bonusAfter} 堂\n" +
                  $"備註：{note}";

        await SendToStudentParentsAsync(
            studentId,
            "CreditsChanged",
            $"credits-change:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}#tab-parent-ledger",
            nowUtc);
    }

    /// <summary>2. 有完成預約時通知（老師後台代約、家長學生自己預約）</summary>
    public async Task NotifyBookingCompletedAsync(int studentId, string slotTimeZh, string progressTitle, string purpose, string operatorName, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;

        var msg = $"【柏拉圖補習班・預約成功】{student.FullName} 已完成輔導預約。\n" +
                  $"預約時段：{slotTimeZh}\n" +
                  $"進度名稱：{progressTitle}\n" +
                  $"到班目的：{purpose}\n" +
                  $"操作人員：{operatorName}\n" +
                  $"提醒：如需取消或改期，請於開課 24 小時前至系統辦理。";

        await SendToStudentParentsAsync(
            studentId,
            "BookingConfirmed",
            $"booking-confirm:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}#tab-parent-records",
            nowUtc);
    }

    /// <summary>3. 通知學生學習狀況(作業不佳、進度落差、觀念不清、輔導未完成、補課確認)需要預約指導</summary>
    public async Task NotifyStudentLearningStatusAsync(int studentId, string conditionType, string progressTitle, string detailNote, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;

        var msg = $"【柏拉圖補習班・學習狀況提醒】{student.FullName} 於進度「{progressTitle}」出現「{conditionType}」狀況。\n" +
                  $"老師備註：{detailNote}\n" +
                  $"建議您儘速至預約系統為孩子預約小組指導或輔導時段，及時補強學習盲點。";

        await SendToStudentParentsAsync(
            studentId,
            "LearningStatusAlert",
            $"learning-status:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}",
            nowUtc);
    }

    /// <summary>4. 提醒預約後仍未預約</summary>
    public async Task NotifyUnbookedReminderAsync(int studentId, string reason, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;

        var msg = $"【柏拉圖補習班・未預約提醒】{student.FullName} 的學習補強通知（{reason}）目前仍未完成預約時段，請儘速登入系統完成預約，以利老師安排進度。";

        await SendToStudentParentsAsync(
            studentId,
            "UnbookedReminder",
            $"unbooked-remind:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}",
            nowUtc);
    }

    /// <summary>5. 提醒兩日後有預約，記得到班或尚有一日可以取消改期</summary>
    public async Task NotifyTwoDaysBeforeReminderAsync(int studentId, string slotTimeZh, string progressTitle, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;

        var msg = $"【柏拉圖補習班・到班提醒】{student.FullName} 於兩日後有預約輔導課堂！\n" +
                  $"課堂時段：{slotTimeZh}（{progressTitle}）\n" +
                  $"請準時到班。若因故需取消或改期，尚有 1 日寬限期（開課 24 小時前免費取消退堂）。";

        await SendToStudentParentsAsync(
            studentId,
            "TwoDaysReminder",
            $"two-days-remind:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}#tab-parent-records",
            nowUtc);
    }

    /// <summary>6. 提醒團體輔導新進度開放預約了</summary>
    public async Task NotifyNewGroupProgressAsync(string progressCode, string progressTitle, DateTime? nowUtc = null)
    {
        var parents = await _db.Users
            .Where(u => u.Role == UserRole.Parent && !string.IsNullOrEmpty(u.LineUserId))
            .ToListAsync();

        var msg = $"【柏拉圖補習班・新進度公告】團體輔導新進度「{progressCode} {progressTitle}」現已開放預約！\n" +
                  $"團體輔導免費開放，歡迎儘早至線上預約時段選課。";

        foreach (var p in parents)
        {
            if (_lineAuth != null && !string.IsNullOrWhiteSpace(p.LineUserId))
            {
                await _lineAuth.SendPushMessageAsync(p.LineUserId, msg);
            }
        }
    }

    /// <summary>7. 候補成功也要通知</summary>
    public async Task NotifyWaitlistSuccessAsync(int studentId, string slotTimeZh, string progressTitle, DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId);
        if (student == null) return;

        var msg = $"【柏拉圖補習班・候補成功】恭喜！{student.FullName} 登記候補的時段已釋出空位並為您保留。\n" +
                  $"時段：{slotTimeZh}（{progressTitle}）\n" +
                  $"名額已保留，請於 30 分鐘內登入網站確認預約！";

        await SendToStudentParentsAsync(
            studentId,
            "WaitlistPromoted",
            $"waitlist-promoted:{studentId}:{Guid.NewGuid():N}",
            msg,
            $"/Parent?studentId={studentId}",
            nowUtc);
    }

    /// <summary>8. 每日排程課前提醒：掃描 24 小時內即將開始之輔導時段發送 LINE 提醒</summary>
    public async Task<int> ProcessUpcomingLessonRemindersAsync(DateTime? nowUtc = null)
    {
        var timestamp = nowUtc ?? DateTime.UtcNow;
        var next24h = timestamp.AddHours(24);

        var upcomingBookings = await _db.Bookings
            .Include(b => b.Student)
            .Include(b => b.TutoringSlot)
            .ThenInclude(s => s!.CourseProgress)
            .Where(b => b.Status == BookingStatus.Confirmed &&
                        b.TutoringSlot != null &&
                        b.TutoringSlot.StartsAtUtc >= timestamp &&
                        b.TutoringSlot.StartsAtUtc <= next24h)
            .ToListAsync();

        var sentCount = 0;
        foreach (var b in upcomingBookings)
        {
            var slot = b.TutoringSlot!;
            var slotTimeZh = BookingService.ToTaipeiTime(slot.StartsAtUtc).ToString("yyyy/MM/dd HH:mm");
            var progressTitle = slot.CourseProgress?.Title ?? "輔導課堂";
            var dateStr = BookingService.ToTaipeiTime(slot.StartsAtUtc).ToString("yyyyMMdd");
            var idempotencyKey = $"lesson-remind:{b.Id}:{dateStr}";

            var alreadySent = await _db.LineNotificationLogs.AnyAsync(l => l.IdempotencyKey == idempotencyKey);
            if (!alreadySent)
            {
                var msg = $"【柏拉圖補習班・課前提醒】{b.Student?.FullName} 於今日/明日有預約輔導課堂！\n" +
                          $"課堂時段：{slotTimeZh}（{progressTitle}）\n" +
                          $"請提醒孩子準時到班參加輔導。";

                await SendToStudentParentsAsync(
                    b.StudentId,
                    "LessonPreReminder",
                    idempotencyKey,
                    msg,
                    $"/Parent?studentId={b.StudentId}#tab-parent-records",
                    timestamp);

                sentCount++;
            }
        }
        return sentCount;
    }
}
