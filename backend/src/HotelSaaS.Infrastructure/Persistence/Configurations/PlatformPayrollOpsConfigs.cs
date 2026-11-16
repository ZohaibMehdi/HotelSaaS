using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class PayrollRunConfig : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> b)
    {
        b.ToTable("PayrollRuns");
        b.HasKey(x => x.PayrollRunId);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.TotalGross).HasPrecision(18, 2);
        b.Property(x => x.TotalDeductions).HasPrecision(18, 2);
        b.Property(x => x.TotalNet).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasMany(x => x.Items).WithOne(i => i.PayrollRun!)
            .HasForeignKey(i => i.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PayrollItemConfig : IEntityTypeConfiguration<PayrollItem>
{
    public void Configure(EntityTypeBuilder<PayrollItem> b)
    {
        b.ToTable("PayrollItems");
        b.HasKey(x => x.PayrollItemId);
        b.HasIndex(x => new { x.PayrollRunId, x.EmployeeId }).IsUnique();
        b.Property(x => x.BasicSalary).HasPrecision(18, 2);
        b.Property(x => x.Allowances).HasPrecision(18, 2);
        b.Property(x => x.OvertimeAmount).HasPrecision(18, 2);
        b.Property(x => x.BonusAmount).HasPrecision(18, 2);
        b.Property(x => x.Deductions).HasPrecision(18, 2);
        b.Property(x => x.AdvanceDeduction).HasPrecision(18, 2);
        b.Property(x => x.LoanDeduction).HasPrecision(18, 2);
        b.Property(x => x.AbsenceDeduction).HasPrecision(18, 2);
        b.Property(x => x.GrossSalary).HasPrecision(18, 2);
        b.Property(x => x.NetSalary).HasPrecision(18, 2);
        b.Property(x => x.Remarks).HasMaxLength(300);
        b.HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class HousekeepingTaskConfig : IEntityTypeConfiguration<HousekeepingTask>
{
    public void Configure(EntityTypeBuilder<HousekeepingTask> b)
    {
        b.ToTable("HousekeepingTasks");
        b.HasKey(x => x.TaskId);
        b.Property(x => x.TaskType).HasConversion<byte>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Room).WithMany()
            .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Booking).WithMany()
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.AssignedToEmployee).WithMany()
            .HasForeignKey(x => x.AssignedToEmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.NotificationId);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Type).HasConversion<byte>();
        b.Property(x => x.Channel).HasConversion<byte>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.EntityType).HasMaxLength(60);
        b.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.AuditLogId);
        b.Property(x => x.Action).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityName).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(50);
        b.Property(x => x.IpAddress).HasMaxLength(50);
        b.Property(x => x.CorrelationId).HasMaxLength(50);
    }
}

public class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenants");
        b.HasKey(x => x.TenantId);
        b.Property(x => x.TenantCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.BusinessName).HasMaxLength(150).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(150);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.ContactEmail).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContactPhone).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Country).HasMaxLength(80);
        b.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        b.HasMany(t => t.Users).WithOne(u => u.Tenant!)
            .HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.UserId);
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.PasswordHash).HasMaxLength(500);
        b.Property(x => x.ResetTokenHash).HasMaxLength(500);
        b.Ignore(x => x.RoleNames);
        b.HasMany(x => x.RefreshTokens).WithOne(t => t.User!)
            .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleAndMiscConfigs
{
}

public class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.RoleId);
        b.Property(x => x.Name).HasMaxLength(30).IsRequired();
    }
}

public class UserRoleConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.TokenId);
        b.Property(x => x.TokenHash).HasMaxLength(500).IsRequired();
    }
}

public class SystemSettingConfig : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("SystemSettings");
        b.HasKey(x => x.SettingId);
        b.Property(x => x.SettingKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(200);
    }
}
