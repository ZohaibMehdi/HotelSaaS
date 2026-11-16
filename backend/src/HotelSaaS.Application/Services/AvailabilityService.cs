using HotelSaaS.Application.Dtos.Bookings;
using HotelSaaS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

/// <summary>Pricing rules live server-side only (PRD rule 7).</summary>
public static class PricingCalculator
{
    public static int Nights(DateTime checkIn, DateTime checkOut)
    {
        var days = (int)Math.Floor((checkOut.Date - checkIn.Date).TotalDays);
        return days < 1 ? 1 : days;
    }

    public static decimal LineTotal(decimal quantity, decimal unitPrice, decimal discountAmount, decimal taxAmount) =>
        Money.Round(quantity * unitPrice - discountAmount + taxAmount);

    /// <summary>Exclusive-tax model: tax = (subtotal - discount) * rate%. Inclusive model yields 0 additive tax.</summary>
    public static (decimal Tax, decimal Total) ApplyTax(decimal subtotal, decimal discount,
        decimal taxRatePercent, bool taxInclusive)
    {
        if (taxInclusive || taxRatePercent <= 0) return (0, Money.Round(subtotal - discount));
        var tax = Money.Round((subtotal - discount) * taxRatePercent / 100m);
        return (tax, Money.Round(subtotal - discount + tax));
    }
}

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailabilityRoomDto>> GetAsync(DateTime from, DateTime to, long? roomTypeId,
        long? corporateCustomerId = null, long? agreementId = null, CancellationToken ct = default);

    /// <summary>Shared conflict scan used by availability display AND booking mutations.</summary>
    Task<HashSet<long>> BlockedRoomIdsAsync(long tenantId, DateTime from, DateTime to,
        long? excludeBookingId, CancellationToken ct);
}

/// <summary>
/// Availability engine. Frontend checks are cosmetic only - every booking
/// mutation re-verifies availability inside a SERIALIZABLE transaction
/// immediately before commit (PRD sections 15 and 43).
/// </summary>
public class AvailabilityService(IAppDbContext db, ICurrentUser currentUser) : IAvailabilityService
{
    public async Task<IReadOnlyList<AvailabilityRoomDto>> GetAsync(DateTime from, DateTime to, long? roomTypeId,
        long? corporateCustomerId = null, long? agreementId = null, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();

        Dictionary<long, decimal>? contractedRates = null;
        if (agreementId.HasValue)
        {
            var agreement = await db.B2BAgreements.AsNoTracking()
                .Include(a => a.Rates)
                .FirstOrDefaultAsync(a => a.AgreementId == agreementId.Value && a.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Agreement not found.");
            if (!agreement.IsApplicableOn(DateOnly.FromDateTime(from)))
                throw new BusinessRuleException("The selected agreement is not active for the requested dates.");
            contractedRates = agreement.Rates.ToDictionary(r => r.RoomTypeId, r => r.ContractedRate);
        }

        var q = db.Rooms.AsNoTracking()
            .Include(r => r.RoomType)
            .Where(r => r.TenantId == tenantId && r.IsActive &&
                        r.Status != RoomStatus.Maintenance && r.Status != RoomStatus.OutOfService);
        if (roomTypeId.HasValue) q = q.Where(r => r.RoomTypeId == roomTypeId.Value);
        var rooms = await q.OrderBy(r => r.RoomNumber).ToListAsync(ct);

        var blockedRoomIds = await BlockedRoomIdsAsync(tenantId, from, to, null, ct);

        return rooms.Select(r => new AvailabilityRoomDto(
            r.RoomId, r.RoomNumber, r.RoomType!.Name, r.RoomTypeId,
            contractedRates != null && contractedRates.TryGetValue(r.RoomTypeId, out var rate)
                ? rate : r.RoomType.BasePrice,
            !blockedRoomIds.Contains(r.RoomId))).ToList();
    }

    /// <summary>Shared conflict scan used by availability display AND booking mutations.</summary>
    public async Task<HashSet<long>> BlockedRoomIdsAsync(long tenantId, DateTime from, DateTime to,
        long? excludeBookingId, CancellationToken ct)
    {
        var blocking = new[] { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn };
        return (await db.BookingRooms.AsNoTracking()
            .Where(br => br.TenantId == tenantId &&
                         (excludeBookingId == null || br.BookingId != excludeBookingId))
            .Join(db.Bookings.Where(b =>
                    b.TenantId == tenantId && b.IsActive && blocking.Contains(b.BookingStatus)),
                br => br.BookingId, b => b.BookingId,
                (br, b) => new { br.RoomId, b.CheckInDate, b.CheckOutDate })
            .Where(x => x.CheckInDate < to && from < x.CheckOutDate)
            .Select(x => x.RoomId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }
}
