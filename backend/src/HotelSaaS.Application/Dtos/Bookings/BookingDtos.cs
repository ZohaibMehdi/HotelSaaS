using FluentValidation;
using HotelSaaS.Application.Dtos.Guests;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Bookings;

public record BookingRoomDto(long BookingRoomId, long RoomId, string RoomNumber, string RoomTypeName,
    decimal RatePerNight, int Nights, decimal TotalRoomCharge);

public record ChargeDto(long ChargeId, ChargeType ChargeType, string Description, DateTime ChargeDate,
    decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount);

public record BookingListItem(long BookingId, string BookingNumber, string GuestName, BookingStatus Status,
    BookingType Type, DateTime CheckInDate, DateTime CheckOutDate, decimal TotalAmount, string Rooms);

public record BookingDto(long BookingId, long TenantId, string BookingNumber, long PrimaryGuestId,
    string PrimaryGuestName, BookingType BookingType, BookingSource BookingSource, BookingStatus Status,
    long? CorporateCustomerId, string? CorporateCustomerName, long? AgreementId, string? AgreementNumber,
    DateTime CheckInDate, DateTime CheckOutDate, DateTime? ActualCheckIn, DateTime? ActualCheckOut,
    int NumberOfAdults, int NumberOfChildren, decimal DiscountAmount, decimal TaxAmount, decimal DepositAmount,
    decimal TotalAmount, string? SpecialRequests, string? Notes, IReadOnlyList<BookingRoomDto> Rooms,
    IReadOnlyList<ChargeDto> Charges, decimal ChargesTotal, string? CancellationReason);

public record AvailabilityRoomDto(long RoomId, string RoomNumber, string RoomTypeName, long RoomTypeId,
    decimal RatePerNight, bool IsAvailable);

public record CreateBookingRequest(
    long GuestId,
    BookingSource BookingSource,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    IReadOnlyList<long> RoomIds,
    int NumberOfAdults = 1,
    int NumberOfChildren = 0,
    BookingType BookingType = BookingType.Individual,
    long? CorporateCustomerId = null,
    long? AgreementId = null,
    decimal DiscountAmount = 0,
    decimal DepositAmount = 0,
    IReadOnlyList<AccompanyGuestDto>? AccompanyGuests = null,
    IReadOnlyList<long>? AdditionalGuestIds = null,
    string? SpecialRequests = null,
    string? Notes = null);

public class CreateBookingValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.GuestId).NotEmpty();
        RuleFor(x => x.CheckInDate).LessThan(x => x.CheckOutDate)
            .WithMessage("Check-out date must be after check-in date.");
        RuleFor(x => x.CheckInDate).Must(d => d >= DateTime.UtcNow.Date.AddDays(-1))
            .WithMessage("Check-in date cannot be in the past.");
        RuleFor(x => x.RoomIds).NotEmpty().WithMessage("At least one room must be selected.");
        RuleFor(x => x.RoomIds).Must(r => r.Count == r.Distinct().Count())
            .WithMessage("Duplicate rooms are not allowed.");
        RuleFor(x => x.NumberOfAdults).InclusiveBetween(1, 20);
        RuleFor(x => x.NumberOfChildren).InclusiveBetween(0, 20);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DepositAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BookingSource).IsInEnum();
        RuleFor(x => x.CorporateCustomerId).NotNull()
            .When(x => x.BookingType == BookingType.Corporate)
            .WithMessage("Corporate bookings require a corporate customer.");
        RuleFor(x => x.AgreementId).NotNull()
            .When(x => x.BookingType == BookingType.Corporate)
            .WithMessage("Corporate bookings require an active agreement.");
        RuleForEach(x => x.AccompanyGuests).SetValidator(new AccompanyGuestValidator());
    }
}

public record AddChargeRequest(ChargeType ChargeType, string Description, decimal Quantity, decimal UnitPrice,
    decimal DiscountAmount = 0);

public class AddChargeValidator : AbstractValidator<AddChargeRequest>
{
    public AddChargeValidator()
    {
        RuleFor(x => x.ChargeType).IsInEnum().NotEqual(ChargeType.Room)
            .WithMessage("Room charges are managed by the system.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(10000);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}

public record CancelBookingRequest(string Reason);

public record WalkInCheckInRequest(
    long GuestId, long RoomId, DateTime CheckOutDate, int NumberOfAdults = 1, int NumberOfChildren = 0,
    decimal DepositAmount = 0, IReadOnlyList<AccompanyGuestDto>? AccompanyGuests = null, string? Notes = null);

public class WalkInCheckInValidator : AbstractValidator<WalkInCheckInRequest>
{
    public WalkInCheckInValidator()
    {
        RuleFor(x => x.GuestId).NotEmpty();
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.CheckOutDate).GreaterThan(DateTime.UtcNow)
            .WithMessage("Check-out date must be in the future.");
        RuleFor(x => x.NumberOfAdults).InclusiveBetween(1, 20);
    }
}
