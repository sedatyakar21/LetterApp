using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Payments;

namespace MektupApp.Domain.Tests;

public class BankTransferTests
{
    // ISO 13616 örneklerinde kullanılan, kontrol hanesi geçerli bir TR IBAN.
    private const string ValidIban = "TR330006100519786457841326";

    private static BankTransferSettings Enabled(decimal rate = 0.05m, int days = 3)
    {
        var s = BankTransferSettings.CreateDefault();
        s.Update(true, rate, "Test Bankası", "BiMektup Ltd.", ValidIban, null, days, "test");
        return s;
    }

    [Fact]
    public void Default_is_disabled_and_not_offered()
    {
        var s = BankTransferSettings.CreateDefault();
        Assert.False(s.Enabled);
        Assert.False(s.IsAvailable);
    }

    [Theory]
    [InlineData("TR33 0006 1005 1978 6457 8413 26")]
    [InlineData("tr330006100519786457841326")]
    public void Iban_is_normalized(string input) =>
        Assert.Equal(ValidIban, BankTransferSettings.NormalizeIban(input));

    [Theory]
    [InlineData("TR340006100519786457841326")] // kontrol hanesi tutmuyor
    [InlineData("DE89370400440532013000")]     // TR değil
    [InlineData("TR33000610051978645784132")]  // eksik hane
    public void Invalid_iban_is_rejected(string iban) =>
        Assert.Throws<DomainException>(() => BankTransferSettings.NormalizeIban(iban));

    [Fact]
    public void Iban_is_displayed_in_groups_of_four()
    {
        Assert.Equal("TR33 0006 1005 1978 6457 8413 26", Enabled().IbanDisplay);
    }

    [Fact]
    public void Enabling_requires_bank_account()
    {
        var s = BankTransferSettings.CreateDefault();
        Assert.Throws<DomainException>(() => s.Update(true, 0.05m, "Banka", "Sahip", null, null, 3, "test"));
        Assert.Throws<DomainException>(() => s.Update(true, 0.05m, null, "Sahip", ValidIban, null, 3, "test"));
        // Kapalıyken hesap bilgisi zorunlu değil.
        s.Update(false, 0.05m, null, null, null, null, 3, "test");
        Assert.False(s.IsAvailable);
    }

    [Theory]
    [InlineData(-0.01, 3)]
    [InlineData(0.51, 3)]
    [InlineData(0.05, 0)]
    [InlineData(0.05, 31)]
    public void Rate_and_days_are_limited(decimal rate, int days) =>
        Assert.Throws<DomainException>(() => Enabled(rate, days));

    [Theory]
    [InlineData(0.05, 290, 14.50)]
    [InlineData(0.075, 333.33, 25.00)]
    [InlineData(0, 290, 0)]
    [InlineData(0.05, 0, 0)]
    public void Discount_is_rounded_to_kurus(decimal rate, decimal amount, decimal expected) =>
        Assert.Equal(expected, Enabled(rate).DiscountFor(amount));

    [Fact]
    public void Payment_keeps_bank_transfer_discount()
    {
        var p = Payment.Start(Guid.NewGuid(), Guid.NewGuid(), BankTransferSettings.ProviderName, PaymentMethod.BankTransfer,
            275.50m, 0, 0, 27.55m, null, 0);
        p.SetBankTransferDiscount(14.5m);
        Assert.True(p.IsBankTransfer);
        Assert.Equal(14.5m, p.BankTransferDiscount);
        Assert.Throws<DomainException>(() => p.SetBankTransferDiscount(-1));

        var card = Payment.Start(Guid.NewGuid(), Guid.NewGuid(), "PayTR", PaymentMethod.Card, 290, 0, 0, 29, null, 0);
        Assert.False(card.IsBankTransfer);
    }
}
