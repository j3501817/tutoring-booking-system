using Microsoft.EntityFrameworkCore;
using PlatoTutoring.Web.Models;

namespace PlatoTutoring.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<ParentStudentLink> ParentStudentLinks => Set<ParentStudentLink>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<CourseProgress> CourseProgresses => Set<CourseProgress>();
    public DbSet<AttendancePurposeOption> AttendancePurposeOptions => Set<AttendancePurposeOption>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<TutoringSlot> TutoringSlots => Set<TutoringSlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<RemittanceAttachment> RemittanceAttachments => Set<RemittanceAttachment>();
    public DbSet<TopUpBatch> TopUpBatches => Set<TopUpBatch>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<DropoutSettlement> DropoutSettlements => Set<DropoutSettlement>();
    public DbSet<HomeworkWatchItem> HomeworkWatchItems => Set<HomeworkWatchItem>();
    public DbSet<LineNotificationLog> LineNotificationLogs => Set<LineNotificationLog>();
    public DbSet<FieldOption> FieldOptions => Set<FieldOption>();
    public DbSet<AcademicHoliday> AcademicHolidays => Set<AcademicHoliday>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 統一將資料表前綴設為 BK_
        modelBuilder.Entity<AppUser>().ToTable("BK_Users");
        modelBuilder.Entity<Student>().ToTable("BK_Students");
        modelBuilder.Entity<ParentStudentLink>().ToTable("BK_ParentStudentLinks");
        modelBuilder.Entity<Semester>().ToTable("BK_Semesters");
        modelBuilder.Entity<CourseProgress>().ToTable("BK_CourseProgresses");
        modelBuilder.Entity<AttendancePurposeOption>().ToTable("BK_AttendancePurposeOptions");
        modelBuilder.Entity<SystemSetting>().ToTable("BK_SystemSettings");
        modelBuilder.Entity<TutoringSlot>().ToTable("BK_TutoringSlots");
        modelBuilder.Entity<Booking>().ToTable("BK_Bookings");
        modelBuilder.Entity<WaitlistEntry>().ToTable("BK_WaitlistEntries");
        modelBuilder.Entity<RemittanceAttachment>().ToTable("BK_RemittanceAttachments");
        modelBuilder.Entity<TopUpBatch>().ToTable("BK_TopUpBatches");
        modelBuilder.Entity<CreditLedgerEntry>().ToTable("BK_CreditLedgerEntries");
        modelBuilder.Entity<DropoutSettlement>().ToTable("BK_DropoutSettlements");
        modelBuilder.Entity<HomeworkWatchItem>().ToTable("BK_HomeworkWatchItems");
        modelBuilder.Entity<LineNotificationLog>().ToTable("BK_LineNotificationLogs");
        modelBuilder.Entity<FieldOption>().ToTable("BK_FieldOptions");
        modelBuilder.Entity<FieldOption>().HasIndex(o => o.Category);
        modelBuilder.Entity<FieldOption>().Property(o => o.ParentValue).IsRequired(false);
        modelBuilder.Entity<AcademicHoliday>().ToTable("BK_AcademicHolidays");

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ParentStudentLink>()
            .HasIndex(p => new { p.ParentUserId, p.StudentId })
            .IsUnique();

        modelBuilder.Entity<TopUpBatch>()
            .Property(t => t.AmountNtd)
            .HasPrecision(12, 2);

        modelBuilder.Entity<TopUpBatch>()
            .Property(t => t.UnitPriceNtd)
            .HasPrecision(12, 2);

        modelBuilder.Entity<DropoutSettlement>()
            .Property(d => d.CalculatedRefundAmountNtd)
            .HasPrecision(12, 2);

        modelBuilder.Entity<LineNotificationLog>()
            .HasIndex(l => l.IdempotencyKey)
            .IsUnique();

        // 防止 SQL Server / Azure SQL 因多重串聯刪除路徑 (Multiple Cascade Paths) 報錯
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
