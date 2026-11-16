using System.Reflection;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Infrastructure.Persistence;

/// <summary>
/// Maps EXPLICITLY onto the database-first schema (database/01_schema.sql).
/// The context performs no migrations; schema evolution is owned by versioned
/// SQL scripts (PRD section 4.1 / rule 3).
/// </summary>
public partial class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<CompanyInformation> CompanyInformations => Set<CompanyInformation>();
    public DbSet<PropertySettings> PropertySettings => Set<PropertySettings>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<AccompanyGuest> AccompanyGuests => Set<AccompanyGuest>();
    public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingRoom> BookingRooms => Set<BookingRoom>();
    public DbSet<CorporateCustomer> CorporateCustomers => Set<CorporateCustomer>();
    public DbSet<B2BAgreement> B2BAgreements => Set<B2BAgreement>();
    public DbSet<B2BAgreementRate> B2BAgreementRates => Set<B2BAgreementRate>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly,
            t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));
        base.OnModelCreating(b);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    private void StampAudit()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity.GetType().GetProperty("CreatedAt") is { } created:
                {
                    var existing = (DateTime?)created.GetValue(entry.Entity);
                    if (existing is null || existing == default)
                        created.SetValue(entry.Entity, utcNow);
                    break;
                }
                case EntityState.Modified when entry.Entity.GetType().GetProperty("UpdatedAt") is { } updated:
                    updated.SetValue(entry.Entity, utcNow);
                    break;
            }
        }
    }
}
