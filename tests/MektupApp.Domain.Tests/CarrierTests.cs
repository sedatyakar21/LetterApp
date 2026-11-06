using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class CarrierTests
{
    private static Letter ShippableLetter()
    {
        var address = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", address, new Recipient("Ayşe", RecipientType.Standard, null, address));
        letter.WriteContent("Merhaba");
        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF");
        letter.StartProduction();
        return letter;
    }

    [Theory]
    [InlineData(47312482, 9)]   // UPU S10 standardındaki örnek: RA473124829GB
    [InlineData(12345678, 5)]
    [InlineData(0, 5)]
    public void S10_check_digit_follows_upu_standard(long serial, int expected)
    {
        Assert.Equal(expected, TrackingCode.S10CheckDigit(serial));
    }

    [Fact]
    public void Builds_valid_s10_barcode()
    {
        Assert.Equal("RA473124829GB", TrackingCode.BuildS10("RA", 47312482, "GB"));
    }

    [Theory]
    [InlineData("rr123456789tr", "RR123456789TR")]
    [InlineData("KP 12345678901", "KP12345678901")]
    public void Accepts_s10_and_cargo_barcodes(string input, string expected)
    {
        Assert.Equal(expected, TrackingCode.Normalize(input));
    }

    [Fact]
    public void Rejects_invalid_barcode()
    {
        Assert.Throws<DomainException>(() => TrackingCode.Normalize("ABC123"));
    }

    [Fact]
    public void Ship_with_carrier_records_reference_and_movements_are_deduplicated()
    {
        var letter = ShippableLetter();
        letter.Ship("RA473124829GB", "PTT", "MEKTUP-20261106-ABC");

        Assert.Equal("PTT", letter.Carrier);
        Assert.Equal("MEKTUP-20261106-ABC", letter.CarrierReference);

        var at = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(letter.RecordCarrierUpdate("KABUL EDİLDİ", at));
        Assert.False(letter.RecordCarrierUpdate("KABUL EDİLDİ", at));
        Assert.True(letter.RecordCarrierUpdate("DAĞITIMA ÇIKARILDI", at.AddHours(5)));
        Assert.Contains(letter.History, h => h.Note == "PTT: DAĞITIMA ÇIKARILDI");

        letter.MarkDelivered("PTT: TESLİM EDİLDİ (Ayşe)");
        Assert.Equal(LetterStatus.Delivered, letter.Status);
        Assert.Throws<DomainException>(() => letter.RecordCarrierUpdate("x", null));
    }
}
