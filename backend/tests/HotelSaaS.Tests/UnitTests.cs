
using HotelSaaS.Application.Security;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Xunit;

namespace HotelSaaS.Tests;

// PRD section 59: unit coverage for pricing, booking rules and security primitives.

public class PricingCalculatorTests
{
    [Theory]
    [InlineData("2026-08-01", "2026-08-04", 3)]
    [InlineData("2026-08-01", "2026-08-02", 1)]
    [InlineData("2026-08-01T15:00", "2026-08-01T20:00", 1)] // same-day stays bill one night minimum
    public void Nights_Computes_Correctly(string inDate, string outDate, int expected)
    {
        var ci = DateTime.Parse(inDate);
        var co = DateTime.Parse(outDate);
        Assert.Equal(expected, HotelSaaS.Application.Services.PricingCalculator.Nights(ci, co));
    }

    [Fact]
    public void ApplyTax_Exclusive_Model_Adds_Tax_On_Net()
    {
        var (tax, total) = HotelSaaS.Application.Services.PricingCalculator.ApplyTax(10_000m, 500m, 5m, taxInclusive: false);
        Assert.Equal(475m, tax);
        Assert.Equal(9_975m, total);
    }

    [Fact]
    public void ApplyTax_Inclusive_Model_Adds_Nothing()
    {
        var (tax, total) = HotelSaaS.Application.Services.PricingCalculator.ApplyTax(10_000m, 0m, 5m, taxInclusive: true);
        Assert.Equal(0m, tax);
        Assert.Equal(10_000m, total);
    }

    [Fact]
    public void LineTotal_Quantity_Discount_Math_Holds()
    {
        // PRD section 23 example: breakfast x2 @350 + laundry 300
        Assert.Equal(700m, HotelSaaS.Application.Services.PricingCalculator.LineTotal(2, 350, 0, 0));
        Assert.Equal(300m, HotelSaaS.Application.Services.PricingCalculator.LineTotal(1, 300, 0, 0));
        Assert.Equal(50m, HotelSaaS.Application.Services.PricingCalculator.LineTotal(2, 100, 150, 0));
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_Verify_Roundtrip_Succeeds()
    {
        var hash = PasswordHasher.Hash("Admin@123");
        Assert.True(PasswordHasher.Verify("Admin@123", hash));
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hash_Is_Salted_And_Never_Plaintext()
    {
        var a = PasswordHasher.Hash("Admin@123");
        var b = PasswordHasher.Hash("Admin@123");
        Assert.NotEqual(a, b);                       // unique salt per hash
        Assert.DoesNotContain("Admin@123", a);       // never plaintext (PRD section 8)
        Assert.StartsWith("PBKDF2-SHA256.", a);
    }

    [Fact]
    public void Verify_Handles_Malformed_Hashes_Safely()
    {
        Assert.False(PasswordHasher.Verify("x", null));
        Assert.False(PasswordHasher.Verify("x", ""));
        Assert.False(PasswordHasher.Verify("x", "not-a-valid-hash"));
    }
}

public class AgreementRuleTests
{
    [Fact]
    public void Active_Agreement_Applies_Only_Within_Date_Range()
    {
        var agreement = new B2BAgreement
        {
            Status = AgreementStatus.Active,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        Assert.True(agreement.IsApplicableOn(new DateOnly(2026, 8, 23)));
        Assert.False(agreement.IsApplicableOn(new DateOnly(2027, 1, 1))); // expired date (PRD rule 11)
    }

    [Fact]
    public void Suspended_Agreement_Never_Applies()
    {
        var agreement = new B2BAgreement
        {
            Status = AgreementStatus.Suspended,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        Assert.False(agreement.IsApplicableOn(new DateOnly(2026, 8, 23)));
    }
}

public class InvoicePaymentMathTests
{
    [Fact]
    public void Payment_Progression_Issued_To_Paid()
    {
        var invoice = new Invoice { Subtotal = 11500m, TotalAmount = 11500m, BalanceDue = 11500m, Status = InvoiceStatus.Issued };

        BookingPaymentSimulator.Apply(invoice, 5000m);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(6500m, invoice.BalanceDue);

        BookingPaymentSimulator.Apply(invoice, 6500m);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.BalanceDue);
    }

    /// <summary>Mirrors BookingService.ApplyPayment semantics used at checkout.</summary>
    private static class BookingPaymentSimulator
    {
        public static void Apply(Invoice invoice, decimal amount) =>
            BookingService.ApplyPayment(invoice, new Payment { Amount = amount });
    }
}

