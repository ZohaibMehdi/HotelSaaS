using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Rooms;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IRoomTypeService
{
    Task<IReadOnlyList<RoomTypeDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<RoomTypeDto> CreateAsync(SaveRoomTypeRequest request, CancellationToken ct = default);
    Task<RoomTypeDto> UpdateAsync(long id, SaveRoomTypeRequest request, CancellationToken ct = default);
    Task DeactivateAsync(long id, CancellationToken ct = default);
}

public class RoomTypeService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IRoomTypeService
{
    public async Task<IReadOnlyList<RoomTypeDto>> GetAllAsync(bool includeInactive = false,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.RoomTypes.AsNoTracking().Where(rt => rt.TenantId == tenantId);
        if (!includeInactive) q = q.Where(rt => rt.IsActive);
        var list = await q.OrderBy(rt => rt.SortOrder).ThenBy(rt => rt.Name).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<RoomTypeDto> CreateAsync(SaveRoomTypeRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveRoomTypeValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        await EnsureNameFreeAsync(tenantId, request.Name, null, ct);

        var rt = new RoomType
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description,
            MaxOccupancy = request.MaxOccupancy,
            BasePrice = request.BasePrice,
            ExtraAdultPrice = request.ExtraAdultPrice,
            ExtraChildPrice = request.ExtraChildPrice,
            NumberOfBeds = request.NumberOfBeds,
            BedConfiguration = request.BedConfiguration,
            Amenities = request.Amenities,
            CreatedBy = currentUser.UserId
        };
        db.RoomTypes.Add(rt);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(RoomType), rt.RoomTypeId, null, new { rt.Name, rt.BasePrice },
            tenantId, ct);
        return Map(rt);
    }

    public async Task<RoomTypeDto> UpdateAsync(long id, SaveRoomTypeRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveRoomTypeValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var rt = await db.RoomTypes.FirstOrDefaultAsync(x => x.RoomTypeId == id && x.TenantId == tenantId, ct)
                 ?? throw new NotFoundException("Room type not found.");
        await EnsureNameFreeAsync(tenantId, request.Name, id, ct);

        rt.Name = request.Name.Trim();
        rt.Description = request.Description;
        rt.MaxOccupancy = request.MaxOccupancy;
        rt.BasePrice = request.BasePrice;
        rt.ExtraAdultPrice = request.ExtraAdultPrice;
        rt.ExtraChildPrice = request.ExtraChildPrice;
        rt.NumberOfBeds = request.NumberOfBeds;
        rt.BedConfiguration = request.BedConfiguration;
        rt.Amenities = request.Amenities;
        rt.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(RoomType), id, null, new { rt.Name, rt.BasePrice }, tenantId, ct);
        return Map(rt);
    }

    public async Task DeactivateAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var rt = await db.RoomTypes.Include(x => x.Rooms)
            .FirstOrDefaultAsync(x => x.RoomTypeId == id && x.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Room type not found.");
        if (rt.Rooms.Any(r => r.Status is RoomStatus.Occupied or RoomStatus.Reserved))
            throw new BusinessRuleException("Cannot deactivate a room type that has reserved or occupied rooms.");

        rt.IsActive = false;
        rt.UpdatedBy = currentUser.UserId;
        foreach (var r in rt.Rooms.Where(r => r.Status is RoomStatus.Available or RoomStatus.Cleaning))
            r.IsActive = false; // cascade soft-delete of idle inventory
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Deactivate", nameof(RoomType), id, new { rt.Name }, null, tenantId, ct);
    }

    private async Task EnsureNameFreeAsync(long tenantId, string name, long? excludeId, CancellationToken ct)
    {
        var taken = await db.RoomTypes.AnyAsync(x =>
            x.TenantId == tenantId && x.IsActive && x.Name == name.Trim() &&
            (excludeId == null || x.RoomTypeId != excludeId), ct);
        if (taken) throw new ConflictException($"Room type '{name}' already exists.");
    }

    private static RoomTypeDto Map(RoomType rt) => new(rt.RoomTypeId, rt.Name, rt.Description, rt.MaxOccupancy,
        rt.BasePrice, rt.ExtraAdultPrice, rt.ExtraChildPrice, rt.NumberOfBeds, rt.BedConfiguration, rt.Amenities,
        rt.IsActive);
}

public interface IRoomService
{
    Task<PagedResult<RoomDto>> SearchAsync(int page, int pageSize, string? search, long? roomTypeId,
        RoomStatus? status, CancellationToken ct = default);
    Task<RoomDto> CreateAsync(SaveRoomRequest request, CancellationToken ct = default);
    Task<RoomDto> UpdateAsync(long id, SaveRoomRequest request, CancellationToken ct = default);
    Task UpdateStatusAsync(long id, UpdateRoomStatusRequest request, CancellationToken ct = default);
    Task UpdateHousekeepingAsync(long id, UpdateHousekeepingRequest request, CancellationToken ct = default);
    Task DeactivateAsync(long id, CancellationToken ct = default);
}

public class RoomService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IRoomService
{
    public async Task<PagedResult<RoomDto>> SearchAsync(int page, int pageSize, string? search, long? roomTypeId,
        RoomStatus? status, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Rooms.AsNoTracking().Where(r => r.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(r => r.RoomNumber.Contains(search));
        if (roomTypeId.HasValue) q = q.Where(r => r.RoomTypeId == roomTypeId.Value);
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        q = q.OrderBy(r => r.RoomNumber);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Include(r => r.RoomType).ToListAsync(ct);
        return new PagedResult<RoomDto>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<RoomDto> CreateAsync(SaveRoomRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveRoomValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var type = await db.RoomTypes.FirstOrDefaultAsync(t =>
                       t.RoomTypeId == request.RoomTypeId && t.TenantId == tenantId && t.IsActive, ct)
                   ?? throw new BadRequestException("Selected room type does not exist for this property.");
        await EnsureNumberFreeAsync(tenantId, request.RoomNumber, null, ct);

        var room = new Room
        {
            TenantId = tenantId,
            RoomTypeId = type.RoomTypeId,
            RoomNumber = request.RoomNumber.Trim(),
            Floor = request.Floor,
            Notes = request.Notes,
            CreatedBy = currentUser.UserId
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(Room), room.RoomId, null, new { room.RoomNumber }, tenantId, ct);
        return Map(room);
    }

    public async Task<RoomDto> UpdateAsync(long id, SaveRoomRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveRoomValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var room = await db.Rooms.Include(r => r.RoomType)
            .FirstOrDefaultAsync(r => r.RoomId == id && r.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Room not found.");
        await EnsureNumberFreeAsync(tenantId, request.RoomNumber, id, ct);

        if (room.Status == RoomStatus.Occupied && room.RoomTypeId != request.RoomTypeId)
            throw new BusinessRuleException("Cannot change the room type of an occupied room.");

        room.RoomTypeId = request.RoomTypeId;
        room.RoomNumber = request.RoomNumber.Trim();
        room.Floor = request.Floor;
        room.Notes = request.Notes;
        room.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(Room), id, null, new { room.RoomNumber, room.Status }, tenantId, ct);
        return Map(room);
    }

    public async Task UpdateStatusAsync(long id, UpdateRoomStatusRequest request, CancellationToken ct = default)
    {
        Validate.Input(new UpdateRoomStatusValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.RoomId == id && r.TenantId == tenantId, ct)
                   ?? throw new NotFoundException("Room not found.");
        var old = room.Status;
        room.Status = request.Status;
        if (request.Status == RoomStatus.Maintenance) room.HousekeepingStatus = HousekeepingStatus.Maintenance;
        room.UpdatedAt = DateTime.UtcNow;
        room.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SetStatus", nameof(Room), id, new { Status = old }, new { Status = request.Status },
            tenantId, ct);
    }

    public async Task UpdateHousekeepingAsync(long id, UpdateHousekeepingRequest request, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.RoomId == id && r.TenantId == tenantId, ct)
                   ?? throw new NotFoundException("Room not found.");
        room.HousekeepingStatus = request.Status;
        // Housekeeping lifecycle (PRD section 31): completing cleaning releases the room.
        if (request.Status is HousekeepingStatus.Clean or HousekeepingStatus.Inspected)
        {
            if (room.Status is RoomStatus.Cleaning or RoomStatus.Reserved or RoomStatus.Available)
                room.Status = RoomStatus.Available;
        }
        else if (request.Status == HousekeepingStatus.Maintenance && room.Status != RoomStatus.Occupied)
        {
            room.Status = RoomStatus.Maintenance;
        }
        room.UpdatedAt = DateTime.UtcNow;
        room.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Housekeeping", nameof(Room), id, null, new { room.HousekeepingStatus, room.Status },
            tenantId, ct);
    }

    public async Task DeactivateAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var room = await db.Rooms.Include(r => r.RoomType).FirstOrDefaultAsync(r => r.RoomId == id && r.TenantId == tenantId, ct)
                   ?? throw new NotFoundException("Room not found.");
        if (room.Status is RoomStatus.Occupied or RoomStatus.Reserved)
            throw new BusinessRuleException("Rooms with active reservations cannot be removed from inventory.");

        room.IsActive = false;
        room.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Deactivate", nameof(Room), id, new { room.RoomNumber }, null, tenantId, ct);
    }

    private async Task EnsureNumberFreeAsync(long tenantId, string number, long? excludeId, CancellationToken ct)
    {
        var taken = await db.Rooms.AnyAsync(r =>
            r.TenantId == tenantId && r.IsActive && r.RoomNumber == number.Trim() &&
            (excludeId == null || r.RoomId != excludeId), ct);
        if (taken) throw new ConflictException($"Room number '{number}' already exists in this property.");
    }

    internal static RoomDto Map(Room r) => new(
        r.RoomId, r.RoomTypeId, r.RoomType?.Name ?? string.Empty, r.RoomNumber, r.Floor, r.Status,
        r.HousekeepingStatus, r.Notes, r.IsActive);
}
