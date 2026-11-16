using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Rooms;

public record RoomTypeDto(long RoomTypeId, string Name, string? Description, int MaxOccupancy,
    decimal BasePrice, decimal ExtraAdultPrice, decimal ExtraChildPrice, int NumberOfBeds,
    string? BedConfiguration, string? Amenities, bool IsActive);

public record SaveRoomTypeRequest(string Name, string? Description, int MaxOccupancy, decimal BasePrice,
    decimal ExtraAdultPrice, decimal ExtraChildPrice, int NumberOfBeds, string? BedConfiguration,
    string? Amenities, bool IsActive);

public class SaveRoomTypeValidator : AbstractValidator<SaveRoomTypeRequest>
{
    public SaveRoomTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MaxOccupancy).InclusiveBetween(1, 20);
        RuleFor(x => x.BasePrice).GreaterThan(0);
        RuleFor(x => x.ExtraAdultPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExtraChildPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NumberOfBeds).InclusiveBetween(0, 10);
    }
}

public record RoomDto(long RoomId, long RoomTypeId, string RoomTypeName, string RoomNumber, int? Floor,
    RoomStatus Status, HousekeepingStatus HousekeepingStatus, string? Notes, bool IsActive);

public record SaveRoomRequest(long RoomTypeId, string RoomNumber, int? Floor, string? Notes, bool IsActive = true);

public class SaveRoomValidator : AbstractValidator<SaveRoomRequest>
{
    public SaveRoomValidator()
    {
        RuleFor(x => x.RoomTypeId).NotEmpty();
        RuleFor(x => x.RoomNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Floor).InclusiveBetween(-5, 200).When(x => x.Floor.HasValue);
    }
}

public record UpdateRoomStatusRequest(RoomStatus Status);
public record UpdateHousekeepingRequest(HousekeepingStatus Status);

public class UpdateRoomStatusValidator : AbstractValidator<UpdateRoomStatusRequest>
{
    public UpdateRoomStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum().Must(s => s != RoomStatus.Occupied)
            .WithMessage("Rooms become Occupied through booking check-in, not direct edits.");
    }
}
