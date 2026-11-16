using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class ChargeConfig : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> b)
    {
        b.ToTable("Charges");
        b.HasKey(x => x.ChargeId);
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.ChargeType).HasConversion<byte>();
        b.Property(x => x.Quantity).HasPrecision(9, 2);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
    }
}

public class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("Invoices");
        b.HasKey(x => x.InvoiceId);
        b.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.InvoiceType).HasConversion<byte>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.DiscountTotal).HasPrecision(18, 2);
        b.Property(x => x.TaxTotal).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.PaidAmount).HasPrecision(18, 2);
        b.Property(x => x.BalanceDue).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);

        b.HasOne(x => x.Booking).WithMany()
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Guest).WithMany()
            .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.CorporateCustomer).WithMany()
            .HasForeignKey(x => x.CorporateCustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Items).WithOne(i => i.Invoice!)
            .HasForeignKey(i => i.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(p => p.Invoice!)
            .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class InvoiceItemConfig : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> b)
    {
        b.ToTable("InvoiceItems");
        b.HasKey(x => x.InvoiceItemId);
        b.Property(x => x.Description).HasMaxLength(200).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(9, 2);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.HasOne(x => x.Invoice).WithMany(i => i.Items)
            .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

public class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(x => x.PaymentId);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.PaymentMethod).HasConversion<byte>();
        b.Property(x => x.TransactionReference).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(300);
    }
}
