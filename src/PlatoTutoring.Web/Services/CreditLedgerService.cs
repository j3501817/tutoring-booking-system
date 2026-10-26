using System.Text;
using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Data;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Services;

public record ReconciliationDiscrepancy(
    int StudentId,
    string StudentName,
    int ActualPaidCredits,
    int LedgerPaidSum,
    int BatchRemainingPaidSum,
    int ActualBonusCredits,
    int LedgerBonusSum,
    string IssueSummary);

public record DropoutSettlementPreview(
    int StudentId,
    string StudentName,
    int TotalRefundablePaidCredits,
    decimal TotalRefundAmountNtd,
    int RemainingBonusCreditsToForfeit,
    List<string> BatchBreakdowns);

public class CreditLedgerService
{
    private readonly AppDbContext _db;

    public CreditLedgerService(AppDbContext db)
    {
        _db = db;
    }

    public static int CalculateProRataSemesterBonus(
        DateOnly enrollmentDate,
        DateOnly semesterStart,
        DateOnly semesterEnd,
        int defaultBonus = 3)
    {
        if (enrollmentDate <= semesterStart) return defaultBonus;
        if (enrollmentDate > semesterEnd) return 0;

        var totalDays = semesterEnd.DayNumber - semesterStart.DayNumber + 1;
        if (totalDays <= 0) return defaultBonus;

        var remainingDays = semesterEnd.DayNumber - enrollmentDate.DayNumber + 1;
        var ratio = (double)remainingDays / totalDays;
        return (int)Math.Round(defaultBonus * ratio, MidpointRounding.AwayFromZero);
    }

    public async Task<TopUpBatch> RecordTopUpAsync(
        int studentId,
        int teacherUserId,
        decimal amountNtd,
        int creditsAdded,
        DateOnly remittanceDate,
        string accountLastFiveDigits,
        string note,
        int? attachmentId = null,
        DateTime? nowUtc = null)
    {
        if (creditsAdded <= 0)
            throw new InvalidOperationException("儲值堂數必須大於 0。");
        if (amountNtd <= 0)
            throw new InvalidOperationException("儲值金額必須大於 0。");
        if (string.IsNullOrWhiteSpace(accountLastFiveDigits) || accountLastFiveDigits.Trim().Length != 5)
            throw new InvalidOperationException("請輸入正確的匯款帳號末五碼（5 碼）。");

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        var teacher = await _db.Users.FindAsync(teacherUserId)
            ?? throw new InvalidOperationException("找不到操作老師帳號。");

        if (teacher.Role != UserRole.Teacher && teacher.Role != UserRole.Admin)
            throw new InvalidOperationException("僅老師或管理員可登錄儲值。");

        var timestamp = nowUtc ?? DateTime.UtcNow;
        var unitPrice = Math.Round(amountNtd / creditsAdded, 2, MidpointRounding.AwayFromZero);

        var batch = new TopUpBatch
        {
            StudentId = studentId,
            RecordedByTeacherId = teacherUserId,
            AmountNtd = amountNtd,
            CreditsAdded = creditsAdded,
            UnitPriceNtd = unitPrice,
            RemainingPaidCredits = creditsAdded,
            RemittanceDate = remittanceDate,
            AccountLastFiveDigits = accountLastFiveDigits.Trim(),
            Note = note.Trim(),
            AttachmentId = attachmentId,
            CreatedAtUtc = timestamp
        };

        _db.TopUpBatches.Add(batch);
        await _db.SaveChangesAsync();

        student.PaidCredits += creditsAdded;

        var entry = new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = LedgerTransactionType.TopUpPaid,
            PaidDelta = creditsAdded,
            BonusDelta = 0,
            PaidBalanceAfter = student.PaidCredits,
            BonusBalanceAfter = student.BonusCredits,
            RelatedTopUpBatchId = batch.Id,
            ReferenceNote = $"儲值登錄：NT${amountNtd:N0} / +{creditsAdded}堂 (末五碼:{batch.AccountLastFiveDigits}) {batch.Note}".Trim(),
            OperatorUserId = teacher.Id,
            OperatorName = teacher.DisplayName,
            CreatedAtUtc = timestamp
        };

        _db.CreditLedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
        return batch;
    }

    public async Task<CreditLedgerEntry> GrantSemesterBonusAsync(
        int studentId,
        int semesterId,
        int operatorUserId,
        DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        var semester = await _db.Semesters.FindAsync(semesterId)
            ?? throw new InvalidOperationException("找不到學期設定。");
        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var bonusToGrant = CalculateProRataSemesterBonus(
            student.EnrollmentDate,
            semester.StartDate,
            semester.EndDate,
            semester.DefaultBonusCredits);

        var timestamp = nowUtc ?? DateTime.UtcNow;
        student.BonusCredits += bonusToGrant;

        var entry = new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = LedgerTransactionType.SemesterBonusGrant,
            PaidDelta = 0,
            BonusDelta = bonusToGrant,
            PaidBalanceAfter = student.PaidCredits,
            BonusBalanceAfter = student.BonusCredits,
            ReferenceNote = $"學期贈送堂數 ({semester.Name})：入班日 {student.EnrollmentDate:yyyy-MM-dd} 按比例核發 +{bonusToGrant} 堂",
            OperatorUserId = op.Id,
            OperatorName = op.DisplayName,
            CreatedAtUtc = timestamp
        };

        _db.CreditLedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<int> GrantSemesterBonusToActiveStudentsAsync(
        int semesterId,
        int operatorUserId,
        DateTime? nowUtc = null)
    {
        var semester = await _db.Semesters.FindAsync(semesterId)
            ?? throw new InvalidOperationException("找不到學期設定。");
        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var timestamp = nowUtc ?? DateTime.UtcNow;

        var activeStudents = await _db.Students
            .Where(s => !s.IsDroppedOut && s.EnrollmentStatus == StudentEnrollmentStatus.Enrolled)
            .ToListAsync();

        var grantedCount = 0;
        foreach (var student in activeStudents)
        {
            var alreadyGranted = await _db.CreditLedgerEntries
                .AnyAsync(e => e.StudentId == student.Id
                               && e.TransactionType == LedgerTransactionType.SemesterBonusGrant
                               && e.ReferenceNote.Contains($"({semester.Name})"));

            if (alreadyGranted) continue;

            var bonusToGrant = CalculateProRataSemesterBonus(
                student.EnrollmentDate,
                semester.StartDate,
                semester.EndDate,
                semester.DefaultBonusCredits);

            student.BonusCredits += bonusToGrant;
            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                StudentId = student.Id,
                TransactionType = LedgerTransactionType.SemesterBonusGrant,
                PaidDelta = 0,
                BonusDelta = bonusToGrant,
                PaidBalanceAfter = student.PaidCredits,
                BonusBalanceAfter = student.BonusCredits,
                ReferenceNote = $"學期贈送堂數 ({semester.Name})：新學期生效自動核發 +{bonusToGrant} 堂 (入班日 {student.EnrollmentDate:yyyy-MM-dd})",
                OperatorUserId = op.Id,
                OperatorName = op.DisplayName,
                CreatedAtUtc = timestamp
            });
            grantedCount++;
        }

        await _db.SaveChangesAsync();
        return grantedCount;
    }

    public async Task<Student> MarkStudentUnrenewedAsync(
        int studentId,
        int operatorUserId,
        string note,
        DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        if (student.IsDroppedOut)
            throw new InvalidOperationException("該學生已退班，無法標記為未續報。");

        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var timestamp = nowUtc ?? DateTime.UtcNow;
        var fromStatus = student.EnrollmentStatus;
        student.EnrollmentStatus = StudentEnrollmentStatus.Unrenewed;

        var currentSem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);

        _db.StudentStatusLogs.Add(new StudentStatusLog
        {
            StudentId = student.Id,
            FromStatus = fromStatus,
            ToStatus = StudentEnrollmentStatus.Unrenewed,
            SemesterId = currentSem?.Id,
            Note = string.IsNullOrWhiteSpace(note) ? "管理員手動標為未續報" : note.Trim(),
            OperatorName = op.DisplayName,
            CreatedAtUtc = timestamp
        });

        await _db.SaveChangesAsync();
        return student;
    }

    public async Task<(Student Student, int BonusGranted)> RenewStudentAsync(
        int studentId,
        int operatorUserId,
        string note,
        DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        if (student.IsDroppedOut)
            throw new InvalidOperationException("該學生已辦理退班結清，無法直接續報；如需入班請以新學生登記。");

        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var timestamp = nowUtc ?? DateTime.UtcNow;
        var fromStatus = student.EnrollmentStatus;
        student.EnrollmentStatus = StudentEnrollmentStatus.Enrolled;

        var currentSem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
        _db.StudentStatusLogs.Add(new StudentStatusLog
        {
            StudentId = student.Id,
            FromStatus = fromStatus,
            ToStatus = StudentEnrollmentStatus.Enrolled,
            SemesterId = currentSem?.Id,
            Note = string.IsNullOrWhiteSpace(note) ? "辦理學期續報" : note.Trim(),
            OperatorName = op.DisplayName,
            CreatedAtUtc = timestamp
        });

        var bonusGranted = 0;
        if (currentSem != null)
        {
            var alreadyGranted = await _db.CreditLedgerEntries
                .AnyAsync(e => e.StudentId == student.Id
                               && e.TransactionType == LedgerTransactionType.SemesterBonusGrant
                               && e.ReferenceNote.Contains($"({currentSem.Name})"));

            if (!alreadyGranted)
            {
                var bonus = CalculateProRataSemesterBonus(
                    student.EnrollmentDate,
                    currentSem.StartDate,
                    currentSem.EndDate,
                    currentSem.DefaultBonusCredits);

                student.BonusCredits += bonus;
                bonusGranted = bonus;

                _db.CreditLedgerEntries.Add(new CreditLedgerEntry
                {
                    StudentId = student.Id,
                    TransactionType = LedgerTransactionType.SemesterBonusGrant,
                    PaidDelta = 0,
                    BonusDelta = bonus,
                    PaidBalanceAfter = student.PaidCredits,
                    BonusBalanceAfter = student.BonusCredits,
                    ReferenceNote = $"學期贈送堂數 ({currentSem.Name})：續報自動核發 +{bonus} 堂 (入班日 {student.EnrollmentDate:yyyy-MM-dd})",
                    OperatorUserId = op.Id,
                    OperatorName = op.DisplayName,
                    CreatedAtUtc = timestamp
                });
            }
        }

        await _db.SaveChangesAsync();
        return (student, bonusGranted);
    }

    public async Task<int> ExpireAllBonusCreditsForSemesterEndAsync(
        int semesterId,
        int operatorUserId,
        DateTime? nowUtc = null)
    {
        var semester = await _db.Semesters.FindAsync(semesterId)
            ?? throw new InvalidOperationException("找不到學期設定。");
        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        var timestamp = nowUtc ?? DateTime.UtcNow;
        var studentsWithBonus = await _db.Students
            .Where(s => s.BonusCredits > 0)
            .ToListAsync();

        var count = 0;
        foreach (var student in studentsWithBonus)
        {
            var expiredBonus = student.BonusCredits;
            student.BonusCredits = 0;
            count++;

            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                StudentId = student.Id,
                TransactionType = LedgerTransactionType.SemesterBonusExpiry,
                PaidDelta = 0,
                BonusDelta = -expiredBonus,
                PaidBalanceAfter = student.PaidCredits,
                BonusBalanceAfter = 0,
                ReferenceNote = $"學期結束贈送堂數歸零 ({semester.Name})：清除未用贈送堂數 -{expiredBonus} 堂",
                OperatorUserId = op.Id,
                OperatorName = op.DisplayName,
                CreatedAtUtc = timestamp
            });
        }

        semester.IsEnded = true;
        semester.IsCurrent = false;
        await _db.SaveChangesAsync();
        return count;
    }

    public async Task<CreditLedgerEntry> ApplyManualAdjustmentAsync(
        int studentId,
        int operatorUserId,
        int paidDelta,
        int bonusDelta,
        string reason,
        DateTime? nowUtc = null)
    {
        if (paidDelta == 0 && bonusDelta == 0)
            throw new InvalidOperationException("付費或贈送堂數異動不可同時為 0。");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("必須填寫沖正／更正或贈送原因。");

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        var op = await _db.Users.FindAsync(operatorUserId)
            ?? throw new InvalidOperationException("找不到操作人。");

        if (op.Role != UserRole.Teacher && op.Role != UserRole.Admin)
            throw new InvalidOperationException("僅老師或管理員可執行堂數更正與發放。");

        if (student.PaidCredits + paidDelta < 0 || student.BonusCredits + bonusDelta < 0)
            throw new InvalidOperationException("沖正後餘額不得小於 0。");

        var timestamp = nowUtc ?? DateTime.UtcNow;

        if (paidDelta < 0)
        {
            await ConsumeFromTopUpBatchesFifoAsync(studentId, -paidDelta);
        }
        else if (paidDelta > 0)
        {
            await RestoreToTopUpBatchesLifoAsync(studentId, paidDelta);
        }

        student.PaidCredits += paidDelta;
        student.BonusCredits += bonusDelta;

        var txType = (paidDelta == 0 && bonusDelta > 0)
            ? LedgerTransactionType.ManualBonusGrant
            : LedgerTransactionType.AdjustmentReversal;

        var entry = new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = txType,
            PaidDelta = paidDelta,
            BonusDelta = bonusDelta,
            PaidBalanceAfter = student.PaidCredits,
            BonusBalanceAfter = student.BonusCredits,
            ReferenceNote = $"堂數沖正／調整：{reason.Trim()}",
            OperatorUserId = op.Id,
            OperatorName = op.DisplayName,
            CreatedAtUtc = timestamp
        };

        _db.CreditLedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<(int BonusUsed, int PaidUsed)> DeductCreditsForBookingAsync(
        int studentId,
        int creditsNeeded,
        int bookingId,
        int operatorUserId,
        string operatorName,
        string slotSummary,
        DateTime? nowUtc = null)
    {
        if (creditsNeeded <= 0)
            return (0, 0);

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");

        if (student.TotalCredits < creditsNeeded)
            throw new InvalidOperationException($"學生可用堂數不足（目前付費 {student.PaidCredits} 堂 + 贈送 {student.BonusCredits} 堂，需 {creditsNeeded} 堂）。");

        var bonusUsed = Math.Min(student.BonusCredits, creditsNeeded);
        var paidUsed = creditsNeeded - bonusUsed;

        if (paidUsed > 0)
        {
            await ConsumeFromTopUpBatchesFifoAsync(studentId, paidUsed);
        }

        student.BonusCredits -= bonusUsed;
        student.PaidCredits -= paidUsed;

        var entry = new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = LedgerTransactionType.BookingDeduct,
            PaidDelta = -paidUsed,
            BonusDelta = -bonusUsed,
            PaidBalanceAfter = student.PaidCredits,
            BonusBalanceAfter = student.BonusCredits,
            RelatedBookingId = bookingId,
            ReferenceNote = $"預約扣堂 ({slotSummary})：優先扣贈送 {bonusUsed} 堂、付費 {paidUsed} 堂",
            OperatorUserId = operatorUserId,
            OperatorName = operatorName,
            CreatedAtUtc = nowUtc ?? DateTime.UtcNow
        };

        _db.CreditLedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
        return (bonusUsed, paidUsed);
    }

    public async Task RefundCreditsForCancellationAsync(
        int studentId,
        int bonusToRefund,
        int paidToRefund,
        int bookingId,
        int operatorUserId,
        string operatorName,
        string slotSummary,
        DateTime? nowUtc = null)
    {
        if (bonusToRefund <= 0 && paidToRefund <= 0)
            return;

        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");

        if (paidToRefund > 0)
        {
            await RestoreToTopUpBatchesLifoAsync(studentId, paidToRefund);
        }

        student.BonusCredits += bonusToRefund;
        student.PaidCredits += paidToRefund;

        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = LedgerTransactionType.CancelRefund,
            PaidDelta = paidToRefund,
            BonusDelta = bonusToRefund,
            PaidBalanceAfter = student.PaidCredits,
            BonusBalanceAfter = student.BonusCredits,
            RelatedBookingId = bookingId,
            ReferenceNote = $"開課前 24 小時取消退堂 ({slotSummary})：退還贈送 +{bonusToRefund} 堂、付費 +{paidToRefund} 堂",
            OperatorUserId = operatorUserId,
            OperatorName = operatorName,
            CreatedAtUtc = nowUtc ?? DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<DropoutSettlementPreview> PreviewDropoutSettlementAsync(int studentId)
    {
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");

        var batches = await _db.TopUpBatches
            .Where(b => b.StudentId == studentId && b.RemainingPaidCredits > 0)
            .OrderBy(b => b.Id)
            .ToListAsync();

        var breakdowns = new List<string>();
        decimal totalRefund = 0m;
        int totalPaidCredits = 0;

        foreach (var b in batches)
        {
            var subTotal = b.RemainingPaidCredits * b.UnitPriceNtd;
            totalRefund += subTotal;
            totalPaidCredits += b.RemainingPaidCredits;
            breakdowns.Add($"批次 #{b.Id} ({b.RemittanceDate:yyyy-MM-dd})：剩餘 {b.RemainingPaidCredits} 堂 × 單價 NT${b.UnitPriceNtd:N0} = NT${subTotal:N0}");
        }

        // 容錯防呆與期初資料同步：若學生主檔付費堂數大於現存儲值批次餘額總和，以基準單價 NT$500 補足結算試算
        if (student.PaidCredits > totalPaidCredits)
        {
            var unbatchedCredits = student.PaidCredits - totalPaidCredits;
            var fallbackUnitPrice = batches.LastOrDefault()?.UnitPriceNtd > 0 ? batches.LastOrDefault()!.UnitPriceNtd : 500m;
            var unbatchedSubTotal = unbatchedCredits * fallbackUnitPrice;
            totalRefund += unbatchedSubTotal;
            totalPaidCredits += unbatchedCredits;
            breakdowns.Add($"系統期初付費堂數：剩餘 {unbatchedCredits} 堂 × 基準單價 NT${fallbackUnitPrice:N0} = NT${unbatchedSubTotal:N0}");
        }

        return new DropoutSettlementPreview(
            student.Id,
            student.FullName,
            totalPaidCredits,
            Math.Round(totalRefund, 0, MidpointRounding.AwayFromZero),
            student.BonusCredits,
            breakdowns);
    }

    public async Task<DropoutSettlement> ExecuteDropoutSettlementAsync(
        int studentId,
        int adminUserId,
        string note,
        DateTime? nowUtc = null)
    {
        var student = await _db.Students.FindAsync(studentId)
            ?? throw new InvalidOperationException("找不到學生資料。");
        var admin = await _db.Users.FindAsync(adminUserId)
            ?? throw new InvalidOperationException("找不到管理員帳號。");

        if (admin.Role != UserRole.Admin)
            throw new InvalidOperationException("僅管理員可執行退班結算。");

        var preview = await PreviewDropoutSettlementAsync(studentId);
        var timestamp = nowUtc ?? DateTime.UtcNow;

        var batches = await _db.TopUpBatches
            .Where(b => b.StudentId == studentId && b.RemainingPaidCredits > 0)
            .ToListAsync();
        foreach (var b in batches)
        {
            b.RemainingPaidCredits = 0;
        }

        var oldPaid = student.PaidCredits;
        var oldBonus = student.BonusCredits;
        student.PaidCredits = 0;
        student.BonusCredits = 0;
        var prevStatus = student.EnrollmentStatus;
        student.IsDroppedOut = true;
        student.DroppedOutAtUtc = timestamp;
        student.EnrollmentStatus = StudentEnrollmentStatus.DroppedOut;

        var currentSem = await _db.Semesters.FirstOrDefaultAsync(s => s.IsCurrent);
        _db.StudentStatusLogs.Add(new StudentStatusLog
        {
            StudentId = student.Id,
            FromStatus = prevStatus,
            ToStatus = StudentEnrollmentStatus.DroppedOut,
            SemesterId = currentSem?.Id,
            Note = $"退班結算結清：{note}".Trim(),
            OperatorName = admin.DisplayName,
            CreatedAtUtc = timestamp
        });

        _db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            StudentId = studentId,
            TransactionType = LedgerTransactionType.DropoutSettlementDeduct,
            PaidDelta = -oldPaid,
            BonusDelta = -oldBonus,
            PaidBalanceAfter = 0,
            BonusBalanceAfter = 0,
            ReferenceNote = $"退班結算歸零：應退付費 {oldPaid} 堂 (NT${preview.TotalRefundAmountNtd:N0})，贈送 {oldBonus} 堂不退費作廢。{note}".Trim(),
            OperatorUserId = admin.Id,
            OperatorName = admin.DisplayName,
            CreatedAtUtc = timestamp
        });

        var settlement = new DropoutSettlement
        {
            StudentId = studentId,
            ProcessedByAdminId = admin.Id,
            ProcessedByAdminName = admin.DisplayName,
            TotalRefundablePaidCredits = preview.TotalRefundablePaidCredits,
            CalculatedRefundAmountNtd = preview.TotalRefundAmountNtd,
            BreakdownSummary = preview.BatchBreakdowns.Count > 0
                ? string.Join("；", preview.BatchBreakdowns)
                : "無剩餘付費堂數批次",
            Note = note.Trim(),
            CreatedAtUtc = timestamp
        };

        _db.DropoutSettlements.Add(settlement);
        await _db.SaveChangesAsync();
        return settlement;
    }

    public async Task<List<ReconciliationDiscrepancy>> RunNightlyReconciliationAsync()
    {
        var students = await _db.Students.ToListAsync();
        var discrepancies = new List<ReconciliationDiscrepancy>();

        foreach (var s in students)
        {
            var ledgerEntries = await _db.CreditLedgerEntries
                .Where(e => e.StudentId == s.Id)
                .ToListAsync();

            var ledgerPaidSum = ledgerEntries.Sum(e => e.PaidDelta);
            var ledgerBonusSum = ledgerEntries.Sum(e => e.BonusDelta);

            var batches = await _db.TopUpBatches
                .Where(b => b.StudentId == s.Id)
                .ToListAsync();
            var batchRemainingSum = batches.Sum(b => b.RemainingPaidCredits);

            if (s.PaidCredits != ledgerPaidSum || s.BonusCredits != ledgerBonusSum || s.PaidCredits != batchRemainingSum)
            {
                discrepancies.Add(new ReconciliationDiscrepancy(
                    s.Id,
                    s.FullName,
                    s.PaidCredits,
                    ledgerPaidSum,
                    batchRemainingSum,
                    s.BonusCredits,
                    ledgerBonusSum,
                    $"主檔付費={s.PaidCredits}, 流水帳付費總和={ledgerPaidSum}, 儲值批次剩餘總和={batchRemainingSum}; 主檔贈送={s.BonusCredits}, 流水帳贈送總和={ledgerBonusSum}"));
            }
        }

        return discrepancies;
    }

    public async Task<string> ExportStudentsCreditsCsvAsync()
    {
        var students = await _db.Students.OrderBy(s => s.Id).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("StudentId,FullName,GradeClass,EnrollmentDate,PaidCredits,BonusCredits,TotalCredits,Status");

        foreach (var s in students)
        {
            var status = s.IsDroppedOut ? "已退班" : "在籍";
            sb.AppendLine($"{s.Id},\"{s.FullName}\",\"{s.GradeClass}\",{s.EnrollmentDate:yyyy-MM-dd},{s.PaidCredits},{s.BonusCredits},{s.TotalCredits},{status}");
        }

        return sb.ToString();
    }

    private async Task ConsumeFromTopUpBatchesFifoAsync(int studentId, int paidCreditsToConsume)
    {
        var batches = await _db.TopUpBatches
            .Where(b => b.StudentId == studentId && b.RemainingPaidCredits > 0)
            .OrderBy(b => b.Id)
            .ToListAsync();

        var remaining = paidCreditsToConsume;
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var take = Math.Min(batch.RemainingPaidCredits, remaining);
            batch.RemainingPaidCredits -= take;
            remaining -= take;
        }
    }

    private async Task RestoreToTopUpBatchesLifoAsync(int studentId, int paidCreditsToRestore)
    {
        var batches = await _db.TopUpBatches
            .Where(b => b.StudentId == studentId && b.RemainingPaidCredits < b.CreditsAdded)
            .OrderByDescending(b => b.Id)
            .ToListAsync();

        var remaining = paidCreditsToRestore;
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var capacity = batch.CreditsAdded - batch.RemainingPaidCredits;
            var addBack = Math.Min(capacity, remaining);
            batch.RemainingPaidCredits += addBack;
            remaining -= addBack;
        }

        if (remaining > 0)
        {
            var latestBatch = await _db.TopUpBatches
                .Where(b => b.StudentId == studentId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync();
            if (latestBatch != null)
            {
                latestBatch.RemainingPaidCredits += remaining;
                latestBatch.CreditsAdded += remaining;
            }
        }
    }
}
