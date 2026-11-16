using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Bookings;
using HotelSaaS.Application.Dtos.Guests;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IGuestService
{
    Task<PagedResult<GuestDto>> SearchAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<GuestDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<GuestDto> CreateAsync(SaveGuestRequest request, CancellationToken ct = default);
    Task<GuestDto> UpdateAsync(long id, SaveGuestRequest request, CancellationToken ct = default);
    Task DeactivateAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<BookingListItem>> HistoryAsync(long id, CancellationToken ct = default);
}

public class GuestService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IGuestService
{
    public async Task<PagedResult<GuestDto>> SearchAsync(int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Guests.AsNoTracking().Where(g => g.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(g =>
                g.FirstName.Contains(s) || (g.LastName != null && g.LastName.Contains(s)) ||
                (g.Phone != null && g.Phone.Contains(s)) || (g.Email != null && g.Email.Contains(s)) ||
                (g.IdNumber != null && g.IdNumber.Contains(s)));
        }
        q = q.OrderByDescending(g => g.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<GuestDto>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<GuestDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        return Map(await FindAsync(id, tenantId, ct));
    }

    public async Task<GuestDto> CreateAsync(SaveGuestRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveGuestValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var guest = new Guest
        {
            TenantId = tenantId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName?.Trim(),
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Nationality = request.Nationality,
            IdType = request.IdType,
            IdNumber = request.IdNumber?.Trim(),
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            Notes = request.Notes,
            CreatedBy = currentUser.UserId
        };
        db.Guests.Add(guest);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(Guest), guest.GuestId, null,
            new { guest.FirstName, guest.LastName }, tenantId, ct);
        return Map(guest);
    }

    public async Task<GuestDto> UpdateAsync(long id, SaveGuestRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveGuestValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var guest = await FindAsync(id, tenantId, ct);
        var old = new { guest.FirstName, guest.LastName, guest.Phone };

        guest.FirstName = request.FirstName.Trim();
        guest.LastName = request.LastName?.Trim();
        guest.Gender = request.Gender;
        guest.DateOfBirth = request.DateOfBirth;
        guest.Nationality = request.Nationality;
        guest.IdType = request.IdType;
        guest.IdNumber = request.IdNumber?.Trim();
        guest.Phone = request.Phone;
        guest.Email = request.Email;
        guest.Address = request.Address;
        guest.City = request.City;
        guest.Country = request.Country;
        guest.Notes = request.Notes;
        guest.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(Guest), id, old, new { guest.FirstName, guest.LastName },
            tenantId, ct);
        return Map(guest);
    }

    /// <summary>Soft delete only; guests referenced by bookings are retained for history.</summary>
    public async Task DeactivateAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var guest = await FindAsync(id, tenantId, ct);
        var hasActiveStay = await db.Bookings.AnyAsync(b =>
            b.TenantId == tenantId && b.PrimaryGuestId == id &&
            b.BookingStatus == BookingStatus.Pending || b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.CheckedIn, ct);
        if (hasActiveStay)
            throw new BusinessRuleException("This guest has an active booking and cannot be deactivated.");

        guest.IsActive = false;
        guest.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Deactivate", nameof(Guest), id, null, null, tenantId, ct);
    }

    public async Task<IReadOnlyList<BookingListItem>> HistoryAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        await FindAsync(id, tenantId, ct);
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.BookingRooms).ThenInclude(br => br.Room)
            .Where(b => b.TenantId == tenantId && b.PrimaryGuestId == id)
            .OrderByDescending(b => b.CheckInDate)
            .Take(50).ToListAsync(ct);
        return bookings.Select(b => new BookingListItem(
            b.BookingId, b.BookingNumber, $"{b.PrimaryGuest!.FirstName} {b.PrimaryGuest.LastName}".Trim(),
            b.BookingStatus, b.BookingType, b.CheckInDate, b.CheckOutDate, b.TotalAmount,
            string.Join(", ", b.BookingRooms.Select(r => r.Room!.RoomNumber)))).ToList();
    }

    private async Task<Guest> FindAsync(long id, long tenantId, CancellationToken ct) =>
        await db.Guests.FirstOrDefaultAsync(g => g.GuestId == id && g.TenantId == tenantId, ct)
        ?? throw new NotFoundException("Guest not found.");

    private static GuestDto Map(Guest g) => new(g.GuestId, g.FirstName, g.LastName, g.Gender, g.DateOfBirth,
        g.Nationality, g.IdType, g.IdNumber, g.Phone, g.Email, g.Address, g.City, g.Country, g.Notes, g.IsActive, g.CreatedAt);
}


