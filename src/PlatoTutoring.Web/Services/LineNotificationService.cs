using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public class LineNotificationService
{
    private readonly AppDbContext _db;

    public LineNotificationService(AppDbContext db)
    {
        _db = db;
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

            var log = new LineNotificationLog
            {
                RecipientParentUserId = link.ParentUserId,
                LineUserId = link.ParentUser.LineUserId ?? "UNBOUND",
                NotificationType = notificationType,
                IdempotencyKey = key,
                MessageText = messageText,
                ActionUrl = actionUrl,
                Status = string.IsNullOrWhiteSpace(link.ParentUser.LineUserId) ? "Failed_NoLineId" : "Sent",
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
            log.Status = "RetriedSuccess";
        }
        else
        {
            log.Status = "Failed_NoLineId";
        }

        await _db.SaveChangesAsync();
        return log.Status == "RetriedSuccess";
    }
}
