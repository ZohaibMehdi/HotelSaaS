using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Guests;

public record GuestDto(long GuestId, string FirstName, string? LastName, Gender? Gender, DateOnly? DateOfBirth,
    string? Nationality, IdType IdType, string? IdNumber, string? Phone, string? Email, string? Address,
    string? City, string? Country, string? Notes, bool IsActive, DateTime CreatedAt);

public record SaveGuestRequest(string FirstName, string? LastName, Gender? Gender, DateOnly? DateOfBirth,
    string? Nationality, IdType IdType, string? IdNumber, string? Phone, string? Email, string? Address,
    string? City, string? Country, string? Notes);

public class SaveGuestValidator : AbstractValidator<SaveGuestRequest>
{
    public SaveGuestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).MaximumLength(80);
        RuleFor(x => x.IdType).IsInEnum();
        // Business rule: CNIC/Passport number is required for the primary identity types.
        RuleFor(x => x.IdNumber).NotEmpty()
            .When(x => x.IdType is IdType.Cnic or IdType.Passport or IdType.NationalId)
            .WithMessage("Identity number is required for the selected ID type.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("Date of birth must be in the past.");
    }
}

public record AccompanyGuestDto(string FullName, IdType? IdType, string? IdNumber,
    string? Relationship, string? Phone, int? Age);

public class AccompanyGuestValidator : AbstractValidator<AccompanyGuestDto>
{
    public AccompanyGuestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Age).InclusiveBetween(0, 120).When(x => x.Age.HasValue);
    }
}

