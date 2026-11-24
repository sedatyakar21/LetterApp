using MektupApp.Domain.Common;
using MektupApp.Domain.Prisons;

namespace MektupApp.Domain.Tests;

public class PrisonTests
{
    [Fact]
    public void Create_trims_and_keeps_optional_fields_empty()
    {
        var p = Prison.Create("  Sincan T Tipi Kapalı Ceza İnfaz Kurumu ", "Ankara", null, null, null, null);
        Assert.Equal("Sincan T Tipi Kapalı Ceza İnfaz Kurumu", p.Name);
        Assert.Equal("", p.District);
        Assert.Equal("", p.AddressLine);
        Assert.Equal("", p.PostalCode);
        Assert.True(p.IsActive);
        Assert.False(p.HasFullAddress);
    }

    [Fact]
    public void Full_address_needs_district_and_line()
    {
        var p = Prison.Create("Elmadağ Kadın Açık CİK", "Ankara", "Elmadağ", "Kurtuluş Mah. Gürbüz Sok. No:4", "06780", "0312 000 00 00");
        Assert.True(p.HasFullAddress);
        Assert.Equal("06780", p.PostalCode);
    }

    [Theory]
    [InlineData("", "Ankara")]
    [InlineData("Kurum", "")]
    public void Name_and_city_are_required(string name, string city) =>
        Assert.Throws<DomainException>(() => Prison.Create(name, city, null, null, null, null));

    [Fact]
    public void Postal_code_when_given_must_be_five_digits() =>
        Assert.Throws<DomainException>(() => Prison.Create("Kurum", "Ankara", "Sincan", "Adres", "0694", null));

    [Fact]
    public void Can_be_deactivated()
    {
        var p = Prison.Create("Kurum", "Ankara", "Sincan", "Adres", null, null);
        p.Update("Kurum", "Ankara", "Sincan", "Adres", null, null, isActive: false);
        Assert.False(p.IsActive);
    }
}
