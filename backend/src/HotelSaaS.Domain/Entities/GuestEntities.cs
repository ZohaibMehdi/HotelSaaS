using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class Guest
{
    public long GuestId { get; set; }
    public long TenantId { get; set; }
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }
    public IdType IdType { get; set; } = IdType.Cnic;
    public string? IdNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = default!;
}

public class AccompanyGuest
{
    public long AccompanyGuestId { get; set; }
    public long BookingId { get; set; }
    public Booking Booking { get; set; } = default!;
    public long TenantId { get; set; }
    public string FullName { get; set; } = default!;
    public IdType? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? Relationship { get; set; }
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingGuest
{
    public long BookingGuestId { get; set; }
    public long BookingId { get; set; }
    public Booking Booking { get; set; } = default!;
    public long TenantId { get; set; }
    public long GuestId { get; set; }
    public Guest Guest { get; set; } = default!;
}
