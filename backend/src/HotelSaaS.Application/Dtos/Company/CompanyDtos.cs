using FluentValidation;

namespace HotelSaaS.Application.Dtos.Company;

public record CompanyInformationDto(long CompanyInformationId, string BusinessName, string? DisplayName,
    string? LegalName, string? RegistrationNumber, string? TaxNumber, string? Address, string? City,
    string? Country, string? PostalCode, string? Phone, string? Email, string? Website, string? LogoPath,
    string? InvoiceHeader, string? InvoiceFooter, decimal TaxPercentage, bool TaxEnabled);

public record SaveCompanyInformationRequest(string BusinessName, string? DisplayName, string? LegalName,
    string? RegistrationNumber, string? TaxNumber, string? Address, string? City, string? Country,
    string? PostalCode, string? Phone, string? Email, string? Website, string? InvoiceHeader,
    string? InvoiceFooter, decimal TaxPercentage, bool TaxEnabled);

public class SaveCompanyInformationValidator : AbstractValidator<SaveCompanyInformationRequest>
{
    public SaveCompanyInformationValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).Must(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Website))
            .WithMessage("Website must be a valid absolute URL.");
        RuleFor(x => x.TaxPercentage).InclusiveBetween(0, 100);
    }
}

public record PropertySettingsDto(long PropertySettingsId, string PropertyName, string PropertyType,
    int? NumberOfFloors, TimeOnly CheckInTime, TimeOnly CheckOutTime, string CurrencyCode, string TimeZoneId,
    decimal TaxRatePercent, bool TaxInclusive, string InvoicePrefix, string BookingPrefix,
    bool DefaultBreakfastIncluded, string? BookingPolicyText, string? CancellationPolicyText,
    string? PaymentPolicyText);

public record SavePropertySettingsRequest(string PropertyName, string PropertyType, int? NumberOfFloors,
    TimeOnly CheckInTime, TimeOnly CheckOutTime, string CurrencyCode, string TimeZoneId,
    decimal TaxRatePercent, bool TaxInclusive, string InvoicePrefix, string BookingPrefix,
    bool DefaultBreakfastIncluded, string? BookingPolicyText, string? CancellationPolicyText,
    string? PaymentPolicyText);

public class SavePropertySettingsValidator : AbstractValidator<SavePropertySettingsRequest>
{
    public SavePropertySettingsValidator()
    {
        RuleFor(x => x.PropertyName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PropertyType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NumberOfFloors).InclusiveBetween(1, 200).When(x => x.NumberOfFloors.HasValue);
        RuleFor(x => x.CheckInTime).LessThan(x => x.CheckOutTime)
            .WithMessage("Check-in time must be before check-out time.");
        RuleFor(x => x.CurrencyCode).Length(3);
        RuleFor(x => x.TaxRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.InvoicePrefix).NotEmpty().MaximumLength(10);
        RuleFor(x => x.BookingPrefix).NotEmpty().MaximumLength(10);
    }
}
