using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Company;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface ICompanyService
{
    Task<CompanyInformationDto> GetAsync(CancellationToken ct = default);
    Task<CompanyInformationDto> SaveAsync(SaveCompanyInformationRequest request, CancellationToken ct = default);
    Task<PropertySettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<PropertySettingsDto> SaveSettingsAsync(SavePropertySettingsRequest request, CancellationToken ct = default);
}

public class CompanyService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : ICompanyService
{
    public async Task<CompanyInformationDto> GetAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var info = await db.CompanyInformations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        return info == null
            ? new CompanyInformationDto(0, string.Empty, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, 0, false)
            : Map(info);
    }

    public async Task<CompanyInformationDto> SaveAsync(SaveCompanyInformationRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveCompanyInformationValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var info = await db.CompanyInformations.FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        var isNew = info is null;
        if (isNew)
        {
            info = new CompanyInformation { TenantId = tenantId, CreatedBy = currentUser.UserId };
            db.CompanyInformations.Add(info);
        }

        var old = !isNew ? Snapshot(info!) : null;
        info!.BusinessName = request.BusinessName.Trim();
        info.DisplayName = request.DisplayName;
        info.LegalName = request.LegalName;
        info.RegistrationNumber = request.RegistrationNumber;
        info.TaxNumber = request.TaxNumber;
        info.Address = request.Address;
        info.City = request.City;
        info.Country = request.Country;
        info.PostalCode = request.PostalCode;
        info.Phone = request.Phone;
        info.Email = request.Email;
        info.Website = request.Website;
        info.InvoiceHeader = request.InvoiceHeader;
        info.InvoiceFooter = request.InvoiceFooter;
        info.TaxPercentage = request.TaxPercentage;
        info.TaxEnabled = request.TaxEnabled;
        info.UpdatedBy = currentUser.UserId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(isNew ? "Create" : "Update", nameof(CompanyInformation), info.CompanyInformationId,
            old, Snapshot(info), tenantId, ct);
        return Map(info);
    }

    public async Task<PropertySettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var s = await db.PropertySettings.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);
        return s == null
            ? new PropertySettingsDto(0, string.Empty, "GuestHouse", null, new TimeOnly(14, 0), new TimeOnly(12, 0),
                "PKR", "UTC", 0, false, "INV-", "BK-", false, null, null, null)
            : Map(s);
    }

    public async Task<PropertySettingsDto> SaveSettingsAsync(SavePropertySettingsRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SavePropertySettingsValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var settings = await db.PropertySettings.FirstOrDefaultAsync(p => p.TenantId == tenantId, ct)
                       ?? throw new NotFoundException("Property settings row missing; contact support.");
        var old = Snapshot(settings);

        settings.PropertyName = request.PropertyName.Trim();
        settings.PropertyType = request.PropertyType.Trim();
        settings.NumberOfFloors = request.NumberOfFloors;
        settings.CheckInTime = request.CheckInTime;
        settings.CheckOutTime = request.CheckOutTime;
        settings.CurrencyCode = request.CurrencyCode.ToUpperInvariant();
        settings.TimeZoneId = request.TimeZoneId;
        settings.TaxRatePercent = request.TaxRatePercent;
        settings.TaxInclusive = request.TaxInclusive;
        settings.InvoicePrefix = request.InvoicePrefix;
        settings.BookingPrefix = request.BookingPrefix;
        settings.DefaultBreakfastIncluded = request.DefaultBreakfastIncluded;
        settings.BookingPolicyText = request.BookingPolicyText;
        settings.CancellationPolicyText = request.CancellationPolicyText;
        settings.PaymentPolicyText = request.PaymentPolicyText;
        settings.UpdatedBy = currentUser.UserId;

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(PropertySettings), settings.PropertySettingsId,
            old, Snapshot(settings), tenantId, ct);
        return Map(settings);
    }

    private static CompanyInformationDto Map(CompanyInformation c) => new(
        c.CompanyInformationId, c.BusinessName, c.DisplayName, c.LegalName, c.RegistrationNumber, c.TaxNumber,
        c.Address, c.City, c.Country, c.PostalCode, c.Phone, c.Email, c.Website, c.LogoPath, c.InvoiceHeader,
        c.InvoiceFooter, c.TaxPercentage, c.TaxEnabled);

    private static PropertySettingsDto Map(PropertySettings s) => new(
        s.PropertySettingsId, s.PropertyName, s.PropertyType, s.NumberOfFloors, s.CheckInTime, s.CheckOutTime,
        s.CurrencyCode, s.TimeZoneId, s.TaxRatePercent, s.TaxInclusive, s.InvoicePrefix, s.BookingPrefix,
        s.DefaultBreakfastIncluded, s.BookingPolicyText, s.CancellationPolicyText, s.PaymentPolicyText);

    private static object Snapshot(CompanyInformation c) => new
    { c.BusinessName, c.TaxPercentage, c.TaxEnabled, c.Email, c.Phone };

    private static object Snapshot(PropertySettings s) => new
    { s.PropertyName, s.CheckInTime, s.CheckOutTime, s.TaxRatePercent, s.InvoicePrefix, s.BookingPrefix };
}
