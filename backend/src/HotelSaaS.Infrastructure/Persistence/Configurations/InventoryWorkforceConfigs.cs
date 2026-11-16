using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class RoomTypeConfig : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> b)
    {
        b.ToTable("RoomTypes");
        b.HasKey(x => x.RoomTypeId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.BasePrice).HasPrecision(18, 2);
        b.Property(x => x.ExtraAdultPrice).HasPrecision(18, 2);
        b.Property(x => x.ExtraChildPrice).HasPrecision(18, 2);
        b.Property(x => x.BedConfiguration).HasMaxLength(100);
        b.Property(x => x.Amenities).HasMaxLength(int.MaxValue);

        b.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Rooms)
            .WithOne(r => r.RoomType!)
            .HasForeignKey(r => r.RoomTypeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class RoomConfig : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("Rooms");
        b.HasKey(x => x.RoomId);
        b.Property(x => x.RoomNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.HousekeepingStatus).HasConversion<byte>();
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class DepartmentConfig : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.HasKey(x => x.DepartmentId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasMany(x => x.Employees)
            .WithOne(e => e.Department!)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class EmployeeConfig : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees");
        b.HasKey(x => x.EmployeeId);
        b.Property(x => x.EmployeeCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(80);
        b.Property(x => x.Cnic).HasMaxLength(50);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Designation).HasMaxLength(100);
        b.Property(x => x.EmploymentStatus).HasConversion<byte>();
        b.Property(x => x.BasicSalary).HasPrecision(18, 2);
        b.Property(x => x.BankName).HasMaxLength(120);
        b.Property(x => x.BankAccountNumber).HasMaxLength(60);
        b.Property(x => x.EmergencyContactName).HasMaxLength(120);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
    }
}
