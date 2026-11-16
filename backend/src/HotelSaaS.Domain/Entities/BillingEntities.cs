using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

/// <summary>
/// Folio charge against a stay. PRD sections 22-23: FoodBills are modelled as
/// Charge rows with ChargeType.Food (see vw_FoodBills in the database).
/// </summary>
public class Charge
{
    public long ChargeId { get; set; }
    public long TenantId { get; set; }
    public long BookingId { get; set; }
    public Booking Booking { get; set; } = default!;
    public ChargeType ChargeType { get; set; }
    public string Description { get; set; } = default!;
    public DateTime ChargeDate { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
}

public class Invoice
{
    public long InvoiceId { get; set; }
    public long TenantId { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public long? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public long? GuestId { get; set; }
    public Guest? Guest { get; set; }
    public long? CorporateCustomerId { get; set; }
    public CorporateCustomer? CorporateCustomer { get; set; }
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Guest;
    public DateTime InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Notes { get; set; }
    public long? IssuedBy { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InvoiceItem> Items { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}

public class InvoiceItem
{
    public long InvoiceItemId { get; set; }
    public long InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public long TenantId { get; set; }
    public string Description { get; set; } = default!;
    public DateTime? ChargeDate { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public long? SourceChargeId { get; set; }
}

public class Payment
{
    public long PaymentId { get; set; }
    public long TenantId { get; set; }
    public long InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public long? ReceivedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public bool IsReversed { get; set; }
    public DateTime? ReversedAt { get; set; }
}
