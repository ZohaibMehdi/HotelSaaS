using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class CompanyInformationConfig : IEntityTypeConfiguration<CompanyInformation>
{
    public void Configure(EntityTypeBuilder<CompanyInformation> b)
    {
        b.ToTable("CompanyInformations");
        b.HasKey(x => x.CompanyInformationId);
        b.Property(x => x.BusinessName).HasMaxLength(150).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(150);
        b.Property(x => x.LegalName).HasMaxLength(150);
        b.Property(x => x.RegistrationNumber).HasMaxLength(60);
        b.Property(x => x.TaxNumber).HasMaxLength(60);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Country).HasMaxLength(80);
        b.Property(x => x.PostalCode).HasMaxLength(20);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Website).HasMaxLength(200);
        b.Property(x => x.LogoPath).HasMaxLength(400);
        b.Property(x => x.InvoiceHeader).HasMaxLength(300);
        b.Property(x => x.InvoiceFooter).HasMaxLength(500);
        b.Property(x => x.TaxPercentage).HasPrecision(5, 2);

        b.HasOne(x => x.Tenant)
            .WithOne(t => t.CompanyInformation!)
            .HasForeignKey<CompanyInformation>(x => x.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class PropertySettingsConfig : IEntityTypeConfiguration<PropertySettings>
{
    public void Configure(EntityTypeBuilder<PropertySettings> b)
    {
        b.ToTable("PropertySettings");
        b.HasKey(x => x.PropertySettingsId);
        b.Property(x => x.PropertyName).HasMaxLength(150).IsRequired();
        b.Property(x => x.PropertyType).HasMaxLength(50).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.TaxRatePercent).HasPrecision(5, 2);
        b.Property(x => x.InvoicePrefix).HasMaxLength(10).IsRequired();
        b.Property(x => x.BookingPrefix).HasMaxLength(10).IsRequired();
        b.Property(x => x.BookingPolicyText).HasMaxLength(1000);
        b.Property(x => x.CancellationPolicyText).HasMaxLength(1000);
        b.Property(x => x.PaymentPolicyText).HasMaxLength(1000);

        b.HasOne(x => x.Tenant)
            .WithOne(t => t.PropertySettings!)
            .HasForeignKey<PropertySettings>(x => x.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
