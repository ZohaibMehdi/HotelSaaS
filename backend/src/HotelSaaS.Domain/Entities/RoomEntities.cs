using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class RoomType
{
    public long RoomTypeId { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int MaxOccupancy { get; set; } = 2;
    public decimal BasePrice { get; set; }
    public decimal ExtraAdultPrice { get; set; }
    public decimal ExtraChildPrice { get; set; }
    public int NumberOfBeds { get; set; } = 1;
    public string? BedConfiguration { get; set; }
    public string? Amenities { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = default!;
    public ICollection<Room> Rooms { get; set; } = [];
}

public class Room
{
    public long RoomId { get; set; }
    public long TenantId { get; set; }
    public long RoomTypeId { get; set; }
    public string RoomNumber { get; set; } = default!;
    public int? Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public HousekeepingStatus HousekeepingStatus { get; set; } = HousekeepingStatus.Clean;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public RoomType RoomType { get; set; } = default!;
    public Tenant Tenant { get; set; } = default!;
}
