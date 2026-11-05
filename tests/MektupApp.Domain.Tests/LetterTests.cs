using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class LetterTests
{
    private static readonly PostalAddress Address = new("Atatürk Cad. No:1", "Nilüfer", "Bursa", "16110");

    private static Letter NewDraft(string content = "Merhaba 😊")
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali Veli", Address,
            new Recipient("Ayşe Yılmaz", RecipientType.Standard, null, Address));
        letter.WriteContent(content);
        return letter;
    }

    [Fact]
    public void Soldier_recipient_requires_unit_detail()
    {
        Assert.Throws<DomainException>(() => new Recipient("Mehmet", RecipientType.Soldier, null, Address));
    }

    [Fact]
    public void Empty_letter_cannot_be_paid()
    {
        var letter = NewDraft("   ");
        Assert.Throws<DomainException>(() => letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF"));
    }

    [Fact]
    public void Paid_letter_cannot_be_edited_but_address_can_change()
    {
        var letter = NewDraft();
        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF");

        Assert.Throws<DomainException>(() => letter.WriteContent("değişti"));
        letter.ChangeRecipientAddress(new PostalAddress("Yeni Sok. 5", "Kadıköy", "İstanbul", "34710"));
        Assert.Equal("İstanbul", letter.Recipient.Address.City);
        Assert.Contains(letter.DomainEvents, e => e is LetterPaid);
    }

    [Fact]
    public void Full_lifecycle_raises_shipped_event_and_blocks_address_change()
    {
        var letter = NewDraft();
        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF");
        letter.StartProduction();
        letter.Ship("rr123456789tr");

        Assert.Equal("RR123456789TR", letter.TrackingCode);
        Assert.Contains(letter.DomainEvents, e => e is LetterShipped);
        Assert.Throws<DomainException>(() => letter.ChangeRecipientAddress(Address));
        Assert.Throws<DomainException>(() => letter.Cancel("vazgeçtim"));

        letter.MarkDelivered();
        Assert.Equal(LetterStatus.Delivered, letter.Status);
    }

    [Fact]
    public void Cannot_ship_before_production()
    {
        var letter = NewDraft();
        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF");
        Assert.Throws<DomainException>(() => letter.Ship("RR123456789TR"));
    }

    [Fact]
    public void Photo_limit_is_enforced()
    {
        var letter = NewDraft();
        for (var i = 0; i < Letter.MaxPhotos; i++) letter.AddPhoto($"k{i}.jpg", "a.jpg");
        Assert.Throws<DomainException>(() => letter.AddPhoto("x.jpg", "x.jpg"));
    }

    [Fact]
    public void Pricing_adds_extras_and_mobile_surcharge()
    {
        var letter = NewDraft(new string('a', LetterPages.CharsPerPage * 2 + 1)); // 3 sayfa
        letter.ChangeStyle(LetterStyle.Default with { Paper = PaperColor.Pink });
        letter.ChooseScent(Scent.Lavender);
        letter.AttachPostcard(new Postcard(PostcardCategory.Love, 2));
        letter.AddPhoto("k.jpg", "a.jpg");

        var policy = new LetterPricingPolicy();
        var prices = new PriceList();
        // 290 + 2*30 + 20 + 35 + 15 + 40 = 460
        Assert.Equal(460m, policy.Calculate(letter, PaymentMethod.Card, prices).Total.Amount);
        Assert.Equal(598m, policy.Calculate(letter, PaymentMethod.Mobile, prices).Total.Amount);
    }

    [Fact]
    public void Pricing_settings_reject_invalid_prices()
    {
        var settings = PricingSettings.Create(new PriceList());
        Assert.Throws<DomainException>(() => settings.Update(new PriceList { BasePrice = 0 }, "admin"));
        Assert.Throws<DomainException>(() => settings.Update(new PriceList { MobilePaymentSurchargeRate = 1.5m }, "admin"));

        settings.Update(new PriceList { BasePrice = 320 }, "admin");
        Assert.Equal(320, settings.Prices.BasePrice);
    }

    [Fact]
    public void Paid_letter_records_payment_time()
    {
        var letter = NewDraft();
        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF");
        Assert.NotNull(letter.PaidAt);
    }
}
