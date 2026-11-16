using ValidationException = HotelSaaS.Application.Common.ValidationException;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Billing;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> SearchAsync(int page, int pageSize, string? search, InvoiceStatus? status,
        long? corporateCustomerId, CancellationToken ct = default);
    Task<InvoiceDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PaymentDto> RecordPaymentAsync(long invoiceId, RecordPaymentRequest request,
        bool allowOverpayment = false, CancellationToken ct = default);
    Task CancelAsync(long id, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentDto>> RecentPaymentsAsync(int take = 100, CancellationToken ct = default);
}

public class InvoiceService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    INotificationService notifications) : IInvoiceService
{
    public async Task<PagedResult<InvoiceDto>> SearchAsync(int page, int pageSize, string? search,
        InvoiceStatus? status, long? corporateCustomerId, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Invoices.AsNoTracking()
            .Include(i => i.Guest).Include(i => i.CorporateCustomer).Include(i => i.Booking)
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i => i.InvoiceNumber.Contains(s) ||
                             (i.Guest != null && (i.Guest.FirstName.Contains(s) || i.Guest.LastName!.Contains(s))) ||
                             (i.CorporateCustomer != null && i.CorporateCustomer.CompanyName.Contains(s)));
        }
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (corporateCustomerId.HasValue) q = q.Where(i => i.CorporateCustomerId == corporateCustomerId.Value);
        q = q.OrderByDescending(i => i.InvoiceDate);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<InvoiceDto>(items.Select(MapList).ToList(), page, pageSize, total);
    }

    public async Task<InvoiceDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Items.OrderBy(it => it.InvoiceItemId))
            .Include(i => i.Payments.OrderByDescending(p => p.PaymentDate))
            .Include(i => i.Booking).Include(i => i.Guest).Include(i => i.CorporateCustomer)
            .FirstOrDefaultAsync(i => i.InvoiceId == id && i.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Invoice not found.");
        return MapDetail(invoice);
    }

    /// <summary>
    /// Records a payment against an invoice with balance validation.
    /// Overpayment is rejected unless explicitly allowed (PRD section 25).
    /// </summary>
    public async Task<PaymentDto> RecordPaymentAsync(long invoiceId, RecordPaymentRequest request,
        bool allowOverpayment = false, CancellationToken ct = default)
    {
        Validate.Input(new RecordPaymentValidator(), request);
        var tenantId = currentUser.RequireTenantId();

        await db.BeginTransactionAsync(ct);
        try
        {
            var invoice = await db.Invoices
                .Include(i => i.Payments.Where(p => !p.IsReversed))
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Invoice not found.");

            if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Draft)
                throw new BusinessRuleException($"Cannot record payment on a {invoice.Status} invoice.");

            // Recompute from ledger to stay consistent regardless of stale snapshots.
            invoice.PaidAmount = Money.Round(invoice.Payments.Sum(p => p.Amount));
            invoice.BalanceDue = Money.Round(invoice.TotalAmount - invoice.PaidAmount);

            if (!allowOverpayment && request.Amount > invoice.BalanceDue)
                throw new BusinessRuleException(
                    $"Payment ({request.Amount:N2}) exceeds the outstanding balance ({invoice.BalanceDue:N2}). " +
                    "Allow overpayment explicitly to accept this amount.");

            var payment = new Payment
            {
                TenantId = tenantId,
                InvoiceId = invoice.InvoiceId,
                Amount = request.Amount,
                PaymentMethod = request.Method,
                TransactionReference = request.TransactionReference?.Trim(),
                PaymentDate = DateTime.UtcNow,
                Notes = request.Notes,
                ReceivedBy = currentUser.UserId,
                CreatedBy = currentUser.UserId
            };
            BookingService.ApplyPayment(invoice, payment);

            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("Payment", nameof(Invoice), invoiceId, null,
                new { Amount = request.Amount, Method = request.Method.ToString(), Invoice = invoice.InvoiceNumber },
                tenantId, ct);
            if (invoice.Status == InvoiceStatus.Paid && invoice.GuestId.HasValue)
                await notifications.CreateInAppAsync(null, tenantId,
                    $"Invoice {invoice.InvoiceNumber} fully settled",
                    $"Payment of {request.Amount:N2} received.", NotificationType.PaymentReceipt, ct);

            return ToPaymentDto(payment);
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    /// <summary>Invoices are reversed via cancellation - never deleted (PRD rule 7).</summary>
    public async Task CancelAsync(long id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { ["reason"] = ["Cancellation reason is required."] });
        var tenantId = currentUser.RequireTenantId();
        var invoice = await db.Invoices.Include(i => i.Payments.Where(p => !p.IsReversed))
            .FirstOrDefaultAsync(i => i.InvoiceId == id && i.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Invoice not found.");
        if (invoice.Payments.Count > 0)
            throw new BusinessRuleException("Invoices with recorded payments cannot be cancelled; reverse payments first.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.Notes = $"{invoice.Notes}\nCancelled: {reason}".Trim();
        invoice.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Cancel", nameof(Invoice), id, null,
            new { invoice.InvoiceNumber, Reason = reason }, tenantId, ct);
    }

    public async Task<IReadOnlyList<PaymentDto>> RecentPaymentsAsync(int take = 100, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var list = await db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.PaymentDate).Take(take).ToListAsync(ct);
        return list.Select(ToPaymentDto).ToList();
    }

    private static InvoiceDto MapList(Invoice i) => new(
        i.InvoiceId, i.InvoiceNumber, i.BookingId, i.Booking?.BookingNumber,
        i.GuestId, Name(i.Guest), i.CorporateCustomerId, i.CorporateCustomer?.CompanyName,
        i.InvoiceType, i.InvoiceDate, i.DueDate, i.Subtotal, i.DiscountTotal, i.TaxTotal,
        i.TotalAmount, i.PaidAmount, i.BalanceDue, i.Status, i.Notes, [], []);

    private static InvoiceDto MapDetail(Invoice i) => new(
        i.InvoiceId, i.InvoiceNumber, i.BookingId, i.Booking?.BookingNumber,
        i.GuestId, Name(i.Guest), i.CorporateCustomerId, i.CorporateCustomer?.CompanyName,
        i.InvoiceType, i.InvoiceDate, i.DueDate, i.Subtotal, i.DiscountTotal, i.TaxTotal,
        i.TotalAmount, i.PaidAmount, i.BalanceDue, i.Status, i.Notes,
        i.Items.Select(it => new InvoiceItemDto(it.Description, it.ChargeDate, it.Quantity,
            it.UnitPrice, it.DiscountAmount, it.TaxAmount, it.TotalAmount)).ToList(),
        i.Payments.Select(ToPaymentDto).ToList());

    private static string Name(Guest? g) => g == null ? null : $"{g.FirstName} {g.LastName}".Trim();

    private static PaymentDto ToPaymentDto(Payment p) => new(
        p.PaymentId, p.Amount, p.PaymentMethod, p.TransactionReference, p.PaymentDate, p.Notes, p.IsReversed);
}

