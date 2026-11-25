using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class LetterTests
{
    private static readonly PostalAddress Address = new("Atatürk Cad. No:1", "Nilüfer", "Bursa", "16110");

    [Fact]
    public void Inmate_father_name_and_ward_are_optional()
    {
        var none = new Recipient("Mehmet Demir", RecipientType.Inmate, "Sincan T Tipi", Address, " ", null);
        Assert.Null(none.FatherName);
        Assert.Null(none.Ward);
        Assert.Null(none.Note);

        var wardOnly = new Recipient("Mehmet Demir", RecipientType.Inmate, "Sincan T Tipi", Address, null, "B-12");
        Assert.Equal("Koğuş: B-12", wardOnly.Note);

        var r = new Recipient("Mehmet Demir", RecipientType.Inmate, null, Address, " Ali ", "B-12");
        Assert.Equal("Ali", r.FatherName);
        Assert.Equal("Baba adı: Ali · Koğuş: B-12", r.Note);
    }

    [Fact]
    public void Father_name_and_ward_are_ignored_for_other_types()
    {
        var r = new Recipient("Ayşe", RecipientType.Standard, null, Address, "Ali", "B-12");
        Assert.Null(r.FatherName);
        Assert.Null(r.Ward);
        Assert.Null(r.Note);
    }

    [Fact]
    public void Changing_address_keeps_inmate_details()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali Veli", Address,
            new Recipient("Mehmet Demir", RecipientType.Inmate, "Sincan T Tipi", Address, "Ali", "B-12"));
        letter.ChangeRecipientAddress(new PostalAddress("Adalet Mah.", "Sincan", "Ankara", null));
        Assert.Equal("Ali", letter.Recipient.FatherName);
        Assert.Equal("B-12", letter.Recipient.Ward);
        Assert.Equal("Sincan", letter.Recipient.Address.District);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Postal_code_is_optional(string? postal)
    {
        var a = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", postal);
        Assert.Equal("", a.PostalCode);
        Assert.Equal("Atatürk Cad. 1, Nilüfer / Bursa", a.ToString());
    }

    [Theory]
    [InlineData("1611")]
    [InlineData("16A10")]
    public void Postal_code_when_given_must_be_five_digits(string postal) =>
        Assert.Throws<DomainException>(() => new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", postal));

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
        letter.ChooseScent(new LetterScent(Guid.NewGuid(), "Lavanta"));
        letter.SetPostcards([new Postcard(Guid.NewGuid(), "Gül Bahçesi", "Aşk")]);
        letter.AddPhoto("k.jpg", "a.jpg");

        var policy = new LetterPricingPolicy();
        var prices = new PriceList();
        // 290 + 2*30 + 20 + 35 + 15 + 40 = 460
        Assert.Equal(460m, policy.Calculate(letter, PaymentMethod.Card, prices).Total.Amount);
        Assert.Equal(598m, policy.Calculate(letter, PaymentMethod.Mobile, prices).Total.Amount);
    }

    [Fact]
    public void Documents_are_limited_priced_and_locked_after_payment()
    {
        var letter = NewDraft();
        var first = letter.AddDocument("d0.pdf", "dilekçe.pdf");
        Assert.True(first.IsPdf);
        for (var i = 1; i < Letter.MaxDocuments; i++) letter.AddDocument($"d{i}.png", "belge.png");
        Assert.Throws<DomainException>(() => letter.AddDocument("x.pdf", "x.pdf"));

        letter.RemoveDocument(first.Id);
        Assert.Equal(Letter.MaxDocuments - 1, letter.Documents.Count);
        Assert.Throws<DomainException>(() => letter.RemoveDocument(Guid.NewGuid()));

        // 290 + (sınır − 1) belge × 25
        var remaining = Letter.MaxDocuments - 1;
        var total = 290m + remaining * 25m;
        var breakdown = new LetterPricingPolicy().Calculate(letter, PaymentMethod.Card, new PriceList());
        Assert.Contains(breakdown.Lines, l => l.Description == $"Belge × {remaining}" && l.Amount.Amount == remaining * 25m);
        Assert.Equal(total, breakdown.Total.Amount);

        letter.MarkPaid(new Money(total), PaymentMethod.Card, "REF");
        Assert.Throws<DomainException>(() => letter.AddDocument("y.pdf", "y.pdf"));
    }

    [Fact]
    public void Pricing_settings_reject_invalid_prices()
    {
        var settings = PricingSettings.Create(new PriceList());
        Assert.Throws<DomainException>(() => settings.Update(new PriceList { BasePrice = 0 }, "admin"));
        Assert.Throws<DomainException>(() => settings.Update(new PriceList { MobilePaymentSurchargeRate = 1.5m }, "admin"));
        Assert.Throws<DomainException>(() => settings.Update(new PriceList { Document = -1 }, "admin"));

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
