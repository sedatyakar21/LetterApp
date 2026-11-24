using System.Text.Json;
using MektupApp.Application.Prisons;
using MektupApp.Domain.Prisons;
using MektupApp.Web.Infrastructure;

namespace MektupApp.Web.Tests;

/// <summary>Cezaevi başlangıç verisi (Infrastructure/Seed/cezaevleri.json) il/ilçe listesiyle ve domain kurallarıyla uyumlu olmalı.</summary>
public class PrisonSeedTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MektupApp.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static List<PrisonSeedItem> Load() =>
        JsonSerializer.Deserialize<List<PrisonSeedItem>>(
            File.ReadAllText(Path.Combine(Root, "src", "MektupApp.Infrastructure", "Seed", "cezaevleri.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void Every_item_is_valid_and_uses_known_city_and_district()
    {
        var items = Load();
        var locations = TurkeyLocations.Load(Path.Combine(Root, "src", "MektupApp.Web", "wwwroot"));
        Assert.True(items.Count > 400);

        foreach (var i in items)
        {
            // Domain kurallarına uymayan kayıt açılışta yüklemeyi bozardı.
            Prison.Create(i.Name, i.City, i.District, i.AddressLine, i.PostalCode, i.Phone);

            var province = locations.Find(i.City);
            Assert.True(province?.Name == i.City, $"Bilinmeyen il: {i.City} ({i.Name})");
            if (!string.IsNullOrEmpty(i.District))
                Assert.True(province!.Districts.Contains(i.District), $"{i.City} ilinde olmayan ilçe: {i.District} ({i.Name})");
        }
    }

    [Fact]
    public void Most_items_have_full_address() =>
        Assert.True(Load().Count(i => !string.IsNullOrEmpty(i.District) && !string.IsNullOrEmpty(i.AddressLine)) > 430);
}
