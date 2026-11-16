using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable("Bookings");
        b.HasKey(x => x.BookingId);
        b.Property(x => x.BookingNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.BookingType).HasConversion<byte>();
        b.Property(x => x.BookingSource).HasConversion<byte>();
        b.Property(x => x.BookingStatus).HasConversion<byte>();
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.TaxAmount).HasPrecision(18, 2);
        b.Property(x => x.DepositAmount).HasPrecision(18, 2);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.SpecialRequests).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.CancellationReason).HasMaxLength(300);

        b.HasOne(x => x.PrimaryGuest).WithMany()
            .HasForeignKey(x => x.PrimaryGuestId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.CorporateCustomer).WithMany()
            .HasForeignKey(x => x.CorporateCustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Agreement).WithMany()
            .HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Charges).WithOne(c => c.Booking!)
            .HasForeignKey(c => c.BookingId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BookingRoomConfig : IEntityTypeConfiguration<BookingRoom>
{
    public void Configure(EntityTypeBuilder<BookingRoom> b)
    {
        b.ToTable("BookingRooms");
        b.HasKey(x => x.BookingRoomId);
        // Index mirrors IX_BookingRooms_Room_Booking used by availability conflict scans.
        b.HasIndex(x => new { x.RoomId, x.BookingId });
        b.Property(x => x.RatePerNight).HasPrecision(18, 2);
        b.Property(x => x.TotalRoomCharge).HasPrecision(18, 2);
        b.HasOne(x => x.Booking).WithMany(x => x.BookingRooms)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Room).WithMany()
            .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class CorporateCustomerConfig : IEntityTypeConfiguration<CorporateCustomer>
{
    public void Configure(EntityTypeBuilder<CorporateCustomer> b)
    {
        b.ToTable("CorporateCustomers");
        b.HasKey(x => x.CorporateCustomerId);
        b.Property(x => x.CompanyName).HasMaxLength(150).IsRequired();
        b.Property(x => x.RegistrationNumber).HasMaxLength(60);
        b.Property(x => x.TaxNumber).HasMaxLength(60);
        b.Property(x => x.ContactPerson).HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.BillingAddress).HasMaxLength(300);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class B2BAgreementConfig : IEntityTypeConfiguration<B2BAgreement>
{
    public void Configure(EntityTypeBuilder<B2BAgreement> b)
    {
        b.ToTable("B2BAgreements");
        b.HasKey(x => x.AgreementId);
        b.Property(x => x.AgreementNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.CreditLimit).HasPrecision(18, 2);
        b.Property(x => x.SpecialFacilities).HasMaxLength(500);
        b.Property(x => x.CancellationRules).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.CorporateCustomer).WithMany(c => c.Agreements)
            .HasForeignKey(x => x.CorporateCustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class B2BAgreementRateConfig : IEntityTypeConfiguration<B2BAgreementRate>
{
    public void Configure(EntityTypeBuilder<B2BAgreementRate> b)
    {
        b.ToTable("B2BAgreementRates");
        b.HasKey(x => x.AgreementRateId);
        b.HasIndex(x => new { x.AgreementId, x.RoomTypeId }).IsUnique();
        b.Property(x => x.ContractedRate).HasPrecision(18, 2);
        b.Property(x => x.ExtraAdultRate).HasPrecision(18, 2);
        b.Property(x => x.ExtraChildRate).HasPrecision(18, 2);
        b.HasOne(x => x.Agreement).WithMany(a => a.Rates)
            .HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.RoomType).WithMany()
            .HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}


