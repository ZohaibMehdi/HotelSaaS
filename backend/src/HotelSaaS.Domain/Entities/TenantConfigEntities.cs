namespace HotelSaaS.Domain.Entities;

public class CompanyInformation
{
    public long CompanyInformationId { get; set; }
    public long TenantId { get; set; }
    public string BusinessName { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string? LegalName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }
    public string? InvoiceHeader { get; set; }
    public string? InvoiceFooter { get; set; }
    public decimal TaxPercentage { get; set; }
    public bool TaxEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = default!;
}

public class PropertySettings
{
    public long PropertySettingsId { get; set; }
    public long TenantId { get; set; }
    public string PropertyName { get; set; } = default!;
    public string PropertyType { get; set; } = "GuestHouse";
    public int? NumberOfFloors { get; set; }
    public TimeOnly CheckInTime { get; set; } = new(14, 0);
    public TimeOnly CheckOutTime { get; set; } = new(12, 0);
    public string CurrencyCode { get; set; } = "PKR";
    public string TimeZoneId { get; set; } = "UTC";
    public decimal TaxRatePercent { get; set; }
    public bool TaxInclusive { get; set; }
    public string InvoicePrefix { get; set; } = "INV-";
    public int InvoiceCounter { get; set; }
    public string BookingPrefix { get; set; } = "BK-";
    public int BookingCounter { get; set; } = 1_000_000;
    public bool DefaultBreakfastIncluded { get; set; }
    public string? BookingPolicyText { get; set; }
    public string? CancellationPolicyText { get; set; }
    public string? PaymentPolicyText { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = default!;
}
