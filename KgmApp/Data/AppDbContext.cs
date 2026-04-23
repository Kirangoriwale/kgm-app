using KgmApp.Models;
using Microsoft.EntityFrameworkCore;

namespace KgmApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();

    public DbSet<SubMember> SubMembers => Set<SubMember>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<FeeSetting> FeeSettings => Set<FeeSetting>();

    public DbSet<Meeting> Meetings => Set<Meeting>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<Suggestion> Suggestions => Set<Suggestion>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<RoleMenuPermission> RoleMenuPermissions => Set<RoleMenuPermission>();

    public DbSet<RulesRegulation> RulesRegulations => Set<RulesRegulation>();
    public DbSet<AboutUsContent> AboutUsContents => Set<AboutUsContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.MobileNo).HasMaxLength(10);
            entity.HasIndex(e => e.MobileNo).IsUnique();
            entity.Property(e => e.EmailId).HasMaxLength(256);
            entity.Property(e => e.Address).HasMaxLength(512);
            entity.Property(e => e.LoginPassword).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Designation).HasMaxLength(256);
            entity.Property(e => e.IsFirstLogin).HasDefaultValue(true);
        });

        modelBuilder.Entity<SubMember>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.MobileNo).HasMaxLength(32);
            entity.Property(e => e.EmailId).HasMaxLength(256);
            entity.Property(e => e.Relation).HasMaxLength(64);
            entity.Property(e => e.Designation).HasMaxLength(256);

            entity
                .HasOne(e => e.Member)
                .WithMany(m => m.SubMembers)
                .HasForeignKey(e => e.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Password).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DonorName).HasMaxLength(256);
            entity.Property(e => e.DonorMobile).HasMaxLength(32);
            entity.Property(e => e.PaymentMode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ModifiedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(12, 2);

            entity
                .HasOne(e => e.Member)
                .WithMany()
                .HasForeignKey(e => e.MemberId)
                .OnDelete(DeleteBehavior.SetNull);

            entity
                .HasOne(e => e.SubMember)
                .WithMany()
                .HasForeignKey(e => e.SubMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FeeSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ApplyFromDate).IsRequired();
            entity.Property(e => e.ContributionFee).HasPrecision(12, 2);
            entity.Property(e => e.RegistrationFee).HasPrecision(12, 2);
            entity.HasIndex(e => e.ApplyFromDate).IsUnique();
        });

        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.MinutesOfMeeting).HasMaxLength(4000);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContentHtml).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Suggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MemberName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<LoginLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.MobileNo).HasMaxLength(20).IsRequired();
            entity.Property(e => e.LoginTimeUtc).IsRequired();
        });

        modelBuilder.Entity<RoleMenuPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoleName).HasMaxLength(32).IsRequired();
            entity.Property(e => e.MenuKey).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.RoleName, e.MenuKey }).IsUnique();
        });

        modelBuilder.Entity<RulesRegulation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentHtml).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AboutUsContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentHtml).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MeetingId, e.MemberId }).IsUnique();

            entity
                .HasOne(e => e.Meeting)
                .WithMany(m => m.Attendances)
                .HasForeignKey(e => e.MeetingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Member)
                .WithMany()
                .HasForeignKey(e => e.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
