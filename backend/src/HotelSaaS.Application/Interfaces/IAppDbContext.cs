using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Interfaces;

/// <summary>
/// Data-access surface used by application services. The concrete context maps
/// to the database-first schema; no extra repository layer is introduced where
/// it would add no value (PRD rule 8).
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<CompanyInformation> CompanyInformations { get; }
    DbSet<PropertySettings> PropertySettings { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Department> Departments { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Guest> Guests { get; }
    DbSet<AccompanyGuest> AccompanyGuests { get; }
    DbSet<BookingGuest> BookingGuests { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<BookingRoom> BookingRooms { get; }
    DbSet<CorporateCustomer> CorporateCustomers { get; }
    DbSet<B2BAgreement> B2BAgreements { get; }
    DbSet<B2BAgreementRate> B2BAgreementRates { get; }
    DbSet<Charge> Charges { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollItem> PayrollItems { get; }
    DbSet<HousekeepingTask> HousekeepingTasks { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);

    /// <summary>Executes an atomic counter increment, returning the new value (booking/invoice numbering).</summary>
    Task<int> IncrementCounterAsync(long tenantId, bool bookingCounter, CancellationToken ct);
}
