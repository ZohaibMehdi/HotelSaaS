using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IReportService
{
    Task<IReadOnlyList<OccupancyPointDto>> OccupancyAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<RevenuePointDto>> RevenueAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<RevenueByRoomTypeDto>> RevenueByRoomTypeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<BookingsBySourceDto>> BookingsBySourceAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<(int Cancellations, int NoShows)> CancellationStatsAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IReadOnlyList<ReceivableDto>> ReceivablesAsync(bool corporateOnly, CancellationToken ct = default);
    Task<IReadOnlyList<GuestStatDto>> GuestStatsAsync(int top = 10, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollSummaryDto>> PayrollSummaryAsync(CancellationToken ct = default);
}

public class ReportService(IAppDbContext db, ICurrentUser currentUser) : IReportService
{
    private const int MaxRangeDays = 92; // guard rails for in-memory aggregation

    public async Task<IReadOnlyList<OccupancyPointDto>> OccupancyAsync(DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        (from, to) = Normalize(from, to);
        var tenantId = currentUser.RequireTenantId();
        var totalRooms = await db.Rooms.AsNoTracking().CountAsync(r => r.TenantId == tenantId && r.IsActive, ct);

        var stays = await db.BookingRooms.AsNoTracking()
            .Join(db.Bookings.Where(b => b.TenantId == tenantId &&
                                         (b.BookingStatus == BookingStatus.CheckedIn ||
                                          b.BookingStatus == BookingStatus.CheckedOut)),
                br => br.BookingId, b => b.BookingId, (br, b) => new { br.RoomId, b.CheckInDate, b.CheckOutDate })
            .Where(x => x.CheckInDate < to.AddDays(1) && x.CheckOutDate > from)
            .ToListAsync(ct);

        var result = new List<OccupancyPointDto>();
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var occupied = stays.Count(s => s.CheckInDate <= day.AddDays(1) && s.CheckOutDate > day);
            result.Add(new OccupancyPointDto(DateOnly.FromDateTime(day), totalRooms, occupied,
                totalRooms == 0 ? 0 : Math.Round(occupied * 100m / totalRooms, 1)));
        }
        return result;
    }

    public async Task<IReadOnlyList<RevenuePointDto>> RevenueAsync(DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        (from, to) = Normalize(from, to);
        var tenantId = currentUser.RequireTenantId();
        var toEnd = to.Date.AddDays(1);

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsReversed &&
                        p.PaymentDate >= from && p.PaymentDate < toEnd)
            .GroupBy(p => p.PaymentDate.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Status != InvoiceStatus.Cancelled &&
                        i.InvoiceDate >= from && i.InvoiceDate < toEnd)
            .GroupBy(i => i.InvoiceDate.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(x => x.TotalAmount) })
            .ToListAsync(ct);

        var days = payments.Select(p => p.Date).Union(invoices.Select(i => i.Date)).OrderBy(d => d);
        return days.Select(d => new RevenuePointDto(d,
            payments.FirstOrDefault(p => p.Date == d)?.Amount ?? 0,
            invoices.FirstOrDefault(i => i.Date == d)?.Amount ?? 0)).ToList();
    }

    public async Task<IReadOnlyList<RevenueByRoomTypeDto>> RevenueByRoomTypeAsync(DateTime from, DateTime to,
        CancellationToken ct = default)
    {
        (from, to) = Normalize(from, to);
        var tenantId = currentUser.RequireTenantId();
        var toEnd = to.Date.AddDays(1);

        var rows = await db.Charges.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ChargeType == ChargeType.Room &&
                        c.ChargeDate >= from && c.ChargeDate < toEnd)
            .Join(db.Bookings.Where(b => b.TenantId == tenantId),
                c => c.BookingId, b => b.BookingId, (c, b) => new { c.TotalAmount, b.BookingId })
            .Join(db.BookingRooms, x => x.BookingId, br => br.BookingId, (x, br) => new { x.TotalAmount, br.RoomId })
            .Join(db.Rooms.Where(r => r.TenantId == tenantId),
                x => x.RoomId, r => r.RoomId, (x, r) => new { r.RoomTypeId, x.TotalAmount })
            .Join(db.RoomTypes.Where(rt => rt.TenantId == tenantId),
                x => x.RoomTypeId, rt => rt.RoomTypeId, (x, rt) => new { rt.Name, x.TotalAmount })
            .GroupBy(x => x.Name)
            .Select(g => new { RoomType = g.Key, Amount = g.Sum(v => v.TotalAmount), Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new RevenueByRoomTypeDto(r.RoomType, r.Amount, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<BookingsBySourceDto>> BookingsBySourceAsync(DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Bookings.AsNoTracking().Where(b => b.TenantId == tenantId);
        if (from.HasValue) q = q.Where(b => b.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(b => b.CreatedAt < to.Value.AddDays(1));

        var rows = await q.GroupBy(b => b.BookingSource)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new BookingsBySourceDto(r.Source.ToString(), r.Count)).ToList();
    }

    public async Task<(int Cancellations, int NoShows)> CancellationStatsAsync(DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Bookings.AsNoTracking().Where(b => b.TenantId == tenantId);
        if (from.HasValue) q = q.Where(b => b.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(b => b.CreatedAt < to.Value.AddDays(1));
        var cancellations = await q.CountAsync(b => b.BookingStatus == BookingStatus.Cancelled, ct);
        var noShows = await q.CountAsync(b => b.BookingStatus == BookingStatus.NoShow, ct);
        return (cancellations, noShows);
    }

    public async Task<IReadOnlyList<ReceivableDto>> ReceivablesAsync(bool corporateOnly, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Invoices.AsNoTracking()
            .Include(i => i.Guest).Include(i => i.CorporateCustomer)
            .Where(i => i.TenantId == tenantId && i.IsActive &&
                        (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid));
        if (corporateOnly) q = q.Where(i => i.CorporateCustomerId != null);
        var list = await q.OrderBy(i => i.DueDate ?? DateOnly.FromDateTime(i.InvoiceDate)).Take(200).ToListAsync(ct);
        return list.Select(i => new ReceivableDto(i.InvoiceId, i.InvoiceNumber,
            i.CorporateCustomer?.CompanyName ??
            (i.Guest != null ? $"{i.Guest.FirstName} {i.Guest.LastName}".Trim() : "-"),
            i.CorporateCustomerId != null, i.InvoiceDate, i.DueDate, i.BalanceDue)).ToList();
    }

    public async Task<IReadOnlyList<GuestStatDto>> GuestStatsAsync(int top = 10, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var stats = await db.Bookings.AsNoTracking()
            .Include(b => b.PrimaryGuest)
            .Where(b => b.TenantId == tenantId &&
                        (b.BookingStatus == BookingStatus.CheckedIn || b.BookingStatus == BookingStatus.CheckedOut))
            .GroupBy(b => new { b.PrimaryGuestId, b.PrimaryGuest!.FirstName, LastName = b.PrimaryGuest.LastName })
            .Select(g => new { g.Key, Stays = g.Count(), Spent = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Stays).Take(top)
            .ToListAsync(ct);
        return stats.Select(s => new GuestStatDto(s.Key.PrimaryGuestId,
            $"{s.Key.FirstName} {s.Key.LastName}".Trim(), s.Stays, s.Spent)).ToList();
    }

    public async Task<IReadOnlyList<PayrollSummaryDto>> PayrollSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var runs = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.PayrollMonth).Take(24).ToListAsync(ct);
        return runs.Select(r => new PayrollSummaryDto(r.PayrollMonth.Year, r.PayrollMonth.Month, r.Status,
            r.TotalGross, r.TotalNet, r.Items?.Count ?? 0)).ToList();
    }

    private static (DateTime, DateTime) Normalize(DateTime from, DateTime to)
    {
        if (to < from) (from, to) = (to, from);
        if ((to.Date - from.Date).TotalDays > MaxRangeDays)
            throw new BusinessRuleException($"Reporting range is limited to {MaxRangeDays} days.");
        return (from.Date, to.Date);
    }
}

