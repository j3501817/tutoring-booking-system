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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
