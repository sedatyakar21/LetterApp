using System.Globalization;
using System.Text.Json;

namespace MektupApp.Web.Infrastructure;

public sealed record Province(string Name, IReadOnlyList<string> Districts);

/// <summary>
/// Türkiye'nin 81 ili ve ilçeleri (wwwroot/data/il-ilce.json). Adres formlarındaki açılır listeler
/// ve mobil uygulama bu veriyi kullanır. Liste Türkçe alfabeye göre sıralanır.
/// </summary>
public sealed class TurkeyLocations
{
    public const string RelativePath = "data/il-ilce.json";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly Dictionary<string, Province> _byName;

    public TurkeyLocations(IReadOnlyList<Province> provinces)
    {
        var comparer = StringComparer.Create(Tr, ignoreCase: false);
        Provinces = provinces
            .Select(p => new Province(p.Name, p.Districts.Order(comparer).ToList()))
            .OrderBy(p => p.Name, comparer)
            .ToList();
        _byName = Provinces.ToDictionary(p => Fold(p.Name));
    }

    /// <summary>
    /// Karşılaştırma anahtarı: küçük harf ve Türkçe karakterler sadeleştirilmiş
    /// ("İstanbul", "ISTANBUL", "istanbul" → "istanbul"; "Kadıköy", "Kadikoy" → "kadikoy").
    /// </summary>
    public static string Fold(string value)
    {
        var s = value.Trim().ToLower(Tr);
        Span<char> buffer = stackalloc char[s.Length];
        var n = 0;
        foreach (var c in s)
        {
            buffer[n++] = c switch
            {
                'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                _ => c
            };
        }
        return new string(buffer[..n]);
    }

    public IReadOnlyList<Province> Provinces { get; }

    public static TurkeyLocations Load(string webRootPath)
    {
        using var stream = File.OpenRead(Path.Combine(webRootPath, RelativePath));
        var items = JsonSerializer.Deserialize<List<Province>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("İl/ilçe listesi okunamadı.");
        return new TurkeyLocations(items);
    }

    /// <summary>İli listedeki yazımıyla bulur; büyük/küçük harf ve Türkçe karakter farkını yok sayar.</summary>
    public Province? Find(string? city) =>
        string.IsNullOrWhiteSpace(city) ? null : _byName.GetValueOrDefault(Fold(city));

    /// <summary>İlçenin listedeki yazımı (ör. "Kadikoy" → "Kadıköy"); ilde yoksa null.</summary>
    public string? FindDistrict(Province? province, string? district)
    {
        if (province is null || string.IsNullOrWhiteSpace(district)) return null;
        var key = Fold(district);
        return province.Districts.FirstOrDefault(d => Fold(d) == key);
    }
}
