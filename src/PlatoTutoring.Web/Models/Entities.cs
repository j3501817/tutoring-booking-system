using System.ComponentModel.DataAnnotations;

namespace PlatoTutoring.Web.Models;

public enum UserRole
{
    Parent,
    Teacher,
    Admin
}

public enum SlotType
{
    SmallGroup,      // 小組指導（每進度限 1 堂，扣 1 堂）
    GroupCounseling  // 團體輔導（不限堂數，免費 0 堂）
}

public enum BookingStatus
{
    Confirmed,
    CancelledOnTime,
    CancelledLate,
    Rescheduled
}

public enum AttendanceStatus
{
    Pending,
    Present,
    Absent,
    NoShow
}

public enum WaitlistStatus
{
    Waiting,
    NotifiedHold,
    ConfirmedToBooking,
    Expired,
    Cancelled
}

public enum LedgerTransactionType
{
    TopUpPaid,
    SemesterBonusGrant,
    ManualBonusGrant,
    BookingDeduct,
    CancelRefund,
    AdjustmentReversal,
    SemesterBonusExpiry,
    DropoutSettlementDeduct
}

public class AppUser
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string Username { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string PasswordHash { get; set; } = string.Empty;
    [Required, MaxLength(80)]
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    [MaxLength(40)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? LineUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Student
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string FullName { get; set; } = string.Empty;
    [Required, MaxLength(50)]
    public string GradeClass { get; set; } = string.Empty;
    public DateOnly EnrollmentDate { get; set; }
    public int PaidCredits { get; set; }
    public int BonusCredits { get; set; }
    public bool IsDroppedOut { get; set; }
    public DateTime? DroppedOutAtUtc { get; set; }
    public DateTime? GroupBookingSuspendedUntilUtc { get; set; }

    public int TotalCredits => PaidCredits + BonusCredits;
}

public class ParentStudentLink
{
    public int Id { get; set; }
    public int ParentUserId { get; set; }
    public AppUser? ParentUser { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    [MaxLength(40)]
    public string Relationship { get; set; } = "家長";
}

public class Semester
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DefaultBonusCredits { get; set; } = 3;
    public bool IsCurrent { get; set; }
    public bool IsEnded { get; set; }
}

public class CourseProgress
{
    public int Id { get; set; }
    public int SemesterId { get; set; }
    public Semester? Semester { get; set; }
    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;
    [Required, MaxLength(50)]
    public string GradeClass { get; set; } = string.Empty;
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public bool IsCurrentProgress { get; set; }
}

public class AttendancePurposeOption
{
    public int Id { get; set; }
    [Required, MaxLength(60)]
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SystemSetting
{
    [Key, MaxLength(80)]
    public string Key { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string Value { get; set; } = string.Empty;
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
}

public class TutoringSlot
{
    public int Id { get; set; }
    public int TeacherUserId { get; set; }
    public AppUser? TeacherUser { get; set; }
    public int CourseProgressId { get; set; }
    public CourseProgress? CourseProgress { get; set; }
    [Required, MaxLength(50)]
    public string GradeClass { get; set; } = string.Empty;
    public SlotType SlotType { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int MaxCapacity { get; set; } = 6;
    public int CurrentBookedCount { get; set; }
    public bool IsExtraSlot { get; set; }
    [MaxLength(200)]
    public string Notes { get; set; } = string.Empty;
    public bool IsAttendanceFinalized { get; set; }

    public int RemainingSeats => Math.Max(0, MaxCapacity - CurrentBookedCount);
}

public class Booking
{
    public int Id { get; set; }
    public int TutoringSlotId { get; set; }
    public TutoringSlot? TutoringSlot { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int CreatedByUserId { get; set; }
    [MaxLength(80)]
    public string CreatedByName { get; set; } = string.Empty;
    public UserRole CreatedByRole { get; set; }
    [Required, MaxLength(60)]
    public string PurposeName { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public int CreditsDeducted { get; set; }
    public int BonusCreditsUsed { get; set; }
    public int PaidCreditsUsed { get; set; }
    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAtUtc { get; set; }
    [MaxLength(200)]
    public string? CancelReason { get; set; }
}

public class WaitlistEntry
{
    public int Id { get; set; }
    public int TutoringSlotId { get; set; }
    public TutoringSlot? TutoringSlot { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int ParentUserId { get; set; }
    public AppUser? ParentUser { get; set; }
    [Required, MaxLength(60)]
    public string PurposeName { get; set; } = string.Empty;
    public int Position { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime? NotifiedAtUtc { get; set; }
    public DateTime? HoldExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RemittanceAttachment
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string OriginalFileName { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string StoredFileName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class TopUpBatch
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int RecordedByTeacherId { get; set; }
    public AppUser? RecordedByTeacher { get; set; }
    public decimal AmountNtd { get; set; }
    public int CreditsAdded { get; set; }
    public decimal UnitPriceNtd { get; set; }
    public int RemainingPaidCredits { get; set; }
    public DateOnly RemittanceDate { get; set; }
    [Required, MaxLength(10)]
    public string AccountLastFiveDigits { get; set; } = string.Empty;
    [MaxLength(200)]
    public string Note { get; set; } = string.Empty;
    public int? AttachmentId { get; set; }
    public RemittanceAttachment? Attachment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CreditLedgerEntry
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public LedgerTransactionType TransactionType { get; set; }
    public int PaidDelta { get; set; }
    public int BonusDelta { get; set; }
    public int PaidBalanceAfter { get; set; }
    public int BonusBalanceAfter { get; set; }
    public int? RelatedBookingId { get; set; }
    public int? RelatedTopUpBatchId { get; set; }
    [Required, MaxLength(250)]
    public string ReferenceNote { get; set; } = string.Empty;
    public int OperatorUserId { get; set; }
    [Required, MaxLength(80)]
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DropoutSettlement
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int ProcessedByAdminId { get; set; }
    public string ProcessedByAdminName { get; set; } = string.Empty;
    public int TotalRefundablePaidCredits { get; set; }
    public decimal CalculatedRefundAmountNtd { get; set; }
    public string BreakdownSummary { get; set; } = string.Empty;
    [MaxLength(250)]
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class HomeworkWatchItem
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int TeacherUserId { get; set; }
    public AppUser? TeacherUser { get; set; }
    public int CourseProgressId { get; set; }
    public CourseProgress? CourseProgress { get; set; }
    [Required, MaxLength(200)]
    public string SubjectNote { get; set; } = string.Empty;
    public DateTime FirstNotifiedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SecondReminderSentAtUtc { get; set; }
    public bool IsSecondReminderFlagged { get; set; }
    public bool IsResolvedByBooking { get; set; }
    public int? ResolvedBookingId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LineNotificationLog
{
    public int Id { get; set; }
    public int RecipientParentUserId { get; set; }
    public AppUser? RecipientParentUser { get; set; }
    [MaxLength(100)]
    public string LineUserId { get; set; } = string.Empty;
    [Required, MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;
    [Required, MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(500)]
    public string MessageText { get; set; } = string.Empty;
    [MaxLength(250)]
    public string ActionUrl { get; set; } = string.Empty;
    [MaxLength(40)]
    public string Status { get; set; } = "Sent";
    public int RetryCount { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
