using MektupApp.Web.Infrastructure;

namespace MektupApp.Web.Tests;

/// <summary>Adres formlarındaki il/ilçe listesi: gerçek veri dosyası okunur.</summary>
public class TurkeyLocationsTests
{
    private static readonly Lazy<TurkeyLocations> Data = new(() =>
    {
        // Test çıktısından depo köküne çıkıp web projesinin wwwroot klasörünü bul.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MektupApp.slnx"))) dir = dir.Parent;
        return TurkeyLocations.Load(Path.Combine(dir!.FullName, "src", "MektupApp.Web", "wwwroot"));
    });

    private static TurkeyLocations L => Data.Value;

    [Fact]
    public void Has_81_provinces_and_973_districts()
    {
        Assert.Equal(81, L.Provinces.Count);
        Assert.Equal(973, L.Provinces.Sum(p => p.Districts.Count));
        Assert.Equal(39, L.Find("İstanbul")!.Districts.Count);
        Assert.Equal(25, L.Find("Ankara")!.Districts.Count);
    }

    [Fact]
    public void Sorted_by_turkish_alphabet()
    {
        var names = L.Provinces.Select(p => p.Name).Take(4).ToArray();
        Assert.Equal(["Adana", "Adıyaman", "Afyonkarahisar", "Ağrı"], names);
        // Ç, C'den sonra gelir; Ş, S'den sonra.
        var all = L.Provinces.Select(p => p.Name).ToList();
        Assert.True(all.IndexOf("Çanakkale") > all.IndexOf("Bursa") && all.IndexOf("Çanakkale") < all.IndexOf("Denizli"));
        Assert.True(all.IndexOf("Şanlıurfa") > all.IndexOf("Sivas"));
    }

    [Theory]
    [InlineData("Istanbul", "İstanbul")]
    [InlineData("ISTANBUL", "İstanbul")]
    [InlineData(" izmir ", "İzmir")]
    [InlineData("canakkale", "Çanakkale")]
    [InlineData("Igdir", "Iğdır")]
    public void Finds_province_ignoring_case_and_turkish_letters(string input, string expected) =>
        Assert.Equal(expected, L.Find(input)?.Name);

    [Fact]
    public void Finds_district_spelling()
    {
        Assert.Equal("Kadıköy", L.FindDistrict(L.Find("İstanbul"), "Kadikoy"));
        Assert.Equal("Çankaya", L.FindDistrict(L.Find("Ankara"), "cankaya"));
        Assert.Null(L.FindDistrict(L.Find("Ankara"), "Kadıköy"));
        Assert.Null(L.Find("Atlantis"));
    }
}
