using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class CorporateCustomer
{
    public long CorporateCustomerId { get; set; }
    public long TenantId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? BillingAddress { get; set; }
    public CorporateStatus Status { get; set; } = CorporateStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = default!;
    public ICollection<B2BAgreement> Agreements { get; set; } = [];
}

public class B2BAgreement
{
    public long AgreementId { get; set; }
    public long TenantId { get; set; }
    public long CorporateCustomerId { get; set; }
    public CorporateCustomer CorporateCustomer { get; set; } = default!;
    public string AgreementNumber { get; set; } = default!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AgreementStatus Status { get; set; } = AgreementStatus.Draft;
    public int PaymentTermsDays { get; set; } = 30;
    public decimal? CreditLimit { get; set; }
    public bool IncludesBreakfast { get; set; }
    public string? SpecialFacilities { get; set; }
    public string? CancellationRules { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<B2BAgreementRate> Rates { get; set; } = [];

    /// <summary>True when the agreement covers the given date and is contractually applicable.</summary>
    public bool IsApplicableOn(DateOnly date) =>
        Status == AgreementStatus.Active && StartDate <= date && EndDate >= date;
}

public class B2BAgreementRate
{
    public long AgreementRateId { get; set; }
    public long AgreementId { get; set; }
    public B2BAgreement Agreement { get; set; } = default!;
    public long TenantId { get; set; }
    public long RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = default!;
    public decimal ContractedRate { get; set; }
    public decimal ExtraAdultRate { get; set; }
    public decimal ExtraChildRate { get; set; }
    public bool IncludesBreakfast { get; set; }
    public bool TaxApplicable { get; set; } = true;
}
