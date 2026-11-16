using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotelSaaS.Infrastructure.Persistence.Configurations;

public class GuestConfig : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("Guests");
        b.HasKey(x => x.GuestId);
        b.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(80);
        b.Property(x => x.Gender).HasConversion<byte>();
        b.Property(x => x.Nationality).HasMaxLength(80);
        b.Property(x => x.IdType).HasConversion<byte>();
        b.Property(x => x.IdNumber).HasMaxLength(60);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.Country).HasMaxLength(80);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.Tenant).WithMany()
            .HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class AccompanyGuestConfig : IEntityTypeConfiguration<AccompanyGuest>
{
    public void Configure(EntityTypeBuilder<AccompanyGuest> b)
    {
        b.ToTable("AccompanyGuests");
        b.HasKey(x => x.AccompanyGuestId);
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.IdType).HasConversion<byte>();
        b.Property(x => x.IdNumber).HasMaxLength(60);
        b.Property(x => x.Relationship).HasMaxLength(60);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.HasOne(x => x.Booking).WithMany(x => x.AccompanyGuests)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BookingGuestConfig : IEntityTypeConfiguration<BookingGuest>
{
    public void Configure(EntityTypeBuilder<BookingGuest> b)
    {
        b.ToTable("BookingGuests");
        b.HasKey(x => x.BookingGuestId);
        b.HasIndex(x => new { x.BookingId, x.GuestId }).IsUnique();
        b.HasOne(x => x.Booking).WithMany(x => x.BookingGuests)
            .HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Guest).WithMany()
            .HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.NoAction);
    }
}
