using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.B2B;

public record CorporateCustomerDto(long CorporateCustomerId, string CompanyName, string? RegistrationNumber,
    string? TaxNumber, string? ContactPerson, string? Phone, string? Email, string? Address,
    string? BillingAddress, CorporateStatus Status, string? Notes, bool IsActive);

public record SaveCorporateCustomerRequest(string CompanyName, string? RegistrationNumber, string? TaxNumber,
    string? ContactPerson, string? Phone, string? Email, string? Address, string? BillingAddress,
    CorporateStatus Status, string? Notes);

public class SaveCorporateCustomerValidator : AbstractValidator<SaveCorporateCustomerRequest>
{
    public SaveCorporateCustomerValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public record AgreementRateDto(long AgreementRateId, long RoomTypeId, string RoomTypeName, decimal ContractedRate,
    decimal ExtraAdultRate, decimal ExtraChildRate, bool IncludesBreakfast, bool TaxApplicable);

public record AgreementDto(long AgreementId, long CorporateCustomerId, string CompanyName, string AgreementNumber,
    DateOnly StartDate, DateOnly EndDate, AgreementStatus Status, int PaymentTermsDays, decimal? CreditLimit,
    bool IncludesBreakfast, string? SpecialFacilities, string? CancellationRules, string? Notes,
    IReadOnlyList<AgreementRateDto> Rates);

public record SaveAgreementRequest(long CorporateCustomerId, string AgreementNumber, DateOnly StartDate,
    DateOnly EndDate, AgreementStatus Status, int PaymentTermsDays, decimal? CreditLimit, bool IncludesBreakfast,
    string? SpecialFacilities, string? CancellationRules, string? Notes,
    IReadOnlyList<UpsertAgreementRateRequest> Rates);

public record UpsertAgreementRateRequest(long RoomTypeId, decimal ContractedRate, decimal ExtraAdultRate,
    decimal ExtraChildRate, bool IncludesBreakfast = true, bool TaxApplicable = true);

public class UpsertAgreementRateValidator : AbstractValidator<UpsertAgreementRateRequest>
{
    public UpsertAgreementRateValidator()
    {
        RuleFor(x => x.RoomTypeId).NotEmpty();
        RuleFor(x => x.ContractedRate).GreaterThan(0);
        RuleFor(x => x.ExtraAdultRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExtraChildRate).GreaterThanOrEqualTo(0);
    }
}

public class SaveAgreementValidator : AbstractValidator<SaveAgreementRequest>
{
    public SaveAgreementValidator()
    {
        RuleFor(x => x.CorporateCustomerId).NotEmpty();
        RuleFor(x => x.AgreementNumber).NotEmpty().MaximumLength(40);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Agreement end date must be on or after the start date.");
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Rates).NotEmpty().WithMessage("An agreement requires at least one room type rate.");
        RuleForEach(x => x.Rates).SetValidator(new UpsertAgreementRateValidator());
    }
}
