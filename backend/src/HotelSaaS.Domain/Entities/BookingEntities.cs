using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class Booking
{
    public long BookingId { get; set; }
    public long TenantId { get; set; }
    public string BookingNumber { get; set; } = default!;
    public long PrimaryGuestId { get; set; }
    public Guest PrimaryGuest { get; set; } = default!;
    public BookingType BookingType { get; set; } = BookingType.Individual;
    public BookingSource BookingSource { get; set; } = BookingSource.WalkIn;
    public BookingStatus BookingStatus { get; set; } = BookingStatus.Pending;
    public long? CorporateCustomerId { get; set; }
    public CorporateCustomer? CorporateCustomer { get; set; }
    public long? AgreementId { get; set; }
    public B2BAgreement? Agreement { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public DateTime? ActualCheckIn { get; set; }
    public DateTime? ActualCheckOut { get; set; }
    public int NumberOfAdults { get; set; } = 1;
    public int NumberOfChildren { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? SpecialRequests { get; set; }
    public string? Notes { get; set; }
    public long? CheckedInBy { get; set; }
    public long? CheckedOutBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<BookingRoom> BookingRooms { get; set; } = [];
    public ICollection<BookingGuest> BookingGuests { get; set; } = [];
    public ICollection<AccompanyGuest> AccompanyGuests { get; set; } = [];
    public ICollection<Charge> Charges { get; set; } = [];

    /// <summary>Statuses that release room inventory (PRD rule 5).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public static readonly BookingStatus[] BlockingStatuses =
        { BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn };
}

public class BookingRoom
{
    public long BookingRoomId { get; set; }
    public long BookingId { get; set; }
    public Booking Booking { get; set; } = default!;
    public long TenantId { get; set; }
    public long RoomId { get; set; }
    public Room Room { get; set; } = default!;
    public decimal RatePerNight { get; set; }
    public int Nights { get; set; }
    public decimal TotalRoomCharge { get; set; }
}
