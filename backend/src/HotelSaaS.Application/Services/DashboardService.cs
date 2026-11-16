using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IDashboardService
{
    Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken ct = default);
    Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken ct = default);
}

public class DashboardService(IAppDbContext db, ICurrentUser currentUser) : IDashboardService
{
    public async Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var todayUtc = DateTime.UtcNow.Date;
        var dayStart = todayUtc;
        var dayEnd = todayUtc.AddDays(1);

        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .Select(r => new { r.Status }).ToListAsync(ct);
        int Count(RoomStatus s) => rooms.Count(r => r.Status == s);
        var totalRooms = rooms.Count;

        var arrivals = await ListArrivalsDeparturesAsync(tenantId, BookingStatus.Confirmed,
            b => b.CheckInDate >= dayStart && b.CheckInDate < dayEnd, ct);
        var departures = await ListArrivalsDeparturesAsync(tenantId, BookingStatus.CheckedIn,
            b => b.CheckOutDate >= dayStart && b.CheckOutDate < dayEnd, ct);

        var currentGuests = await db.Bookings.AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.BookingStatus == BookingStatus.CheckedIn)
            .SumAsync(b => (int?)(b.NumberOfAdults + b.NumberOfChildren)) ?? 0;

        var todaysRevenue = await db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsReversed &&
                        p.PaymentDate >= dayStart && p.PaymentDate < dayEnd)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var outstanding = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsActive &&
                        (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid))
            .SumAsync(i => (decimal?)i.BalanceDue) ?? 0m;

        var pendingBookings = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.TenantId == tenantId &&
                             (b.BookingStatus == BookingStatus.Pending ||
                              b.BookingStatus == BookingStatus.Confirmed), ct);

        return new TenantDashboardDto(
            totalRooms,
            Count(RoomStatus.Available), Count(RoomStatus.Occupied), Count(RoomStatus.Reserved),
            Count(RoomStatus.Cleaning), Count(RoomStatus.Maintenance) + Count(RoomStatus.OutOfService),
            arrivals.Count, departures.Count, currentGuests,
            todaysRevenue, outstanding, pendingBookings,
            totalRooms == 0 ? 0 : Math.Round(Count(RoomStatus.Occupied) * 100m / totalRooms, 1),
            arrivals, departures);
    }

    public async Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsSuperAdmin)
            throw new ForbiddenException("Platform dashboard is restricted to SuperAdmin.");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var canConnect = await ((Application.Interfaces.IAppDbContext)db).SystemSettings.AnyAsync(ct);
        sw.Stop();

        var tenants = await db.Tenants.AsNoTracking().ToListAsync(ct);
        var userStats = await db.Users.AsNoTracking()
            .GroupBy(u => u.IsActive).Select(g => new { Active = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalRooms = await db.Rooms.AsNoTracking().CountAsync(r => r.IsActive, ct);
        var totalBookings = await db.Bookings.AsNoTracking().CountAsync(ct);

        return new PlatformDashboardDto(
            tenants.Count,
            tenants.Count(t => t.Status == TenantStatus.Active),
            tenants.Count(t => t.Status is TenantStatus.Inactive),
            tenants.Count(t => t.Status == TenantStatus.Suspended),
            userStats.Sum(u => u.Count),
            userStats.Where(u => u.Active).Sum(u => u.Count),
            totalRooms, totalBookings,
            tenants.OrderByDescending(t => t.CreatedAt).Take(5)
                .Select(t => new RecentTenantDto(t.TenantId, t.TenantCode, t.BusinessName, t.Status, t.CreatedAt))
                .ToList(),
            Math.Round(sw.Elapsed.TotalMilliseconds, 2));
    }

    private async Task<IReadOnlyList<ArrivalDepartureDto>> ListArrivalsDeparturesAsync(
        long tenantId, BookingStatus status, System.Linq.Expressions.Expression<Func<Domain.Entities.Booking, bool>> dateFilter,
        CancellationToken ct)
    {
        var list = await db.Bookings.AsNoTracking()
            .Include(b => b.PrimaryGuest).Include(b => b.BookingRooms).ThenInclude(br => br.Room)
            .Where(b => b.TenantId == tenantId && b.BookingStatus == status)
            .Where(dateFilter)
            .OrderBy(b => b.CheckInDate).Take(20).ToListAsync(ct);
        return list.Select(b => new ArrivalDepartureDto(b.BookingId, b.BookingNumber,
            $"{b.PrimaryGuest!.FirstName} {b.PrimaryGuest.LastName}".Trim(),
            string.Join(", ", b.BookingRooms.Select(r => r.Room!.RoomNumber)),
            status == BookingStatus.Confirmed ? b.CheckInDate : b.CheckOutDate,
            b.BookingStatus)).ToList();
    }
}
