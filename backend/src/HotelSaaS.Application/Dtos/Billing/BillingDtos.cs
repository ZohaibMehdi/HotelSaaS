using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Billing;

public record InvoiceItemDto(string Description, DateTime? ChargeDate, decimal Quantity, decimal UnitPrice,
    decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount);

public record PaymentDto(long PaymentId, decimal Amount, PaymentMethod Method, string? TransactionReference,
    DateTime PaymentDate, string? Notes, bool IsReversed);

public record InvoiceDto(long InvoiceId, string InvoiceNumber, long? BookingId, string? BookingNumber,
    long? GuestId, string? GuestName, long? CorporateCustomerId, string? CorporateCustomerName,
    InvoiceType Type, DateTime InvoiceDate, DateOnly? DueDate, decimal Subtotal, decimal DiscountTotal,
    decimal TaxTotal, decimal TotalAmount, decimal PaidAmount, decimal BalanceDue, InvoiceStatus Status,
    string? Notes, IReadOnlyList<InvoiceItemDto> Items, IReadOnlyList<PaymentDto> Payments);

public record RecordPaymentRequest(decimal Amount, PaymentMethod Method, string? TransactionReference,
    string? Notes, bool AllowOverpayment = false);

public class RecordPaymentValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.TransactionReference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}
