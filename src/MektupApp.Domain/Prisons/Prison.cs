using MektupApp.Domain.Common;

namespace MektupApp.Domain.Prisons;

/// <summary>
/// Ceza infaz kurumu (cezaevi). Alıcı türü "Cezaevi" seçilen mektuplarda il seçilince o ilin kurumları listelenir,
/// kurum seçilince alıcı adresi bu kayıttan doldurulur. Liste yönetim panelinden güncellenir;
/// başlangıç verisi Ceza ve Tevkifevleri Genel Müdürlüğü sitesinden alınmıştır.
/// </summary>
public sealed class Prison : AggregateRoot
{
    public const int NameMax = 200;
    public const int AddressMax = 250;

    private Prison() { } // EF Core

    public string Name { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    /// <summary>Boş olabilir (kaynakta adresi olmayan kurumlar); yönetici tamamlar.</summary>
    public string District { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    /// <summary>Kapalı / pasif kurumlar listede görünmez ama eski mektupların adresi bozulmaz.</summary>
    public bool IsActive { get; private set; } = true;
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Adresi tam mı (il, ilçe ve adres satırı dolu): yalnızca tam kayıtlar formda adresi doldurur.</summary>
    public bool HasFullAddress => District.Length > 0 && AddressLine.Length > 0;

    public static Prison Create(string name, string city, string? district, string? addressLine, string? postalCode, string? phone)
    {
        var p = new Prison();
        p.Update(name, city, district, addressLine, postalCode, phone, isActive: true);
        return p;
    }

    public void Update(string name, string city, string? district, string? addressLine, string? postalCode, string? phone, bool isActive)
    {
        Name = Guard.Required(name, "Kurum adı", NameMax);
        City = Guard.Required(city, "İl", 60);
        District = Guard.Optional(district, "İlçe", 60) ?? string.Empty;
        AddressLine = Guard.Optional(addressLine, "Adres", AddressMax) ?? string.Empty;
        var postal = postalCode?.Trim() ?? string.Empty;
        if (postal.Length > 0 && (postal.Length != 5 || !postal.All(char.IsAsciiDigit)))
            throw new DomainException("Posta kodu 5 haneli olmalıdır.");
        PostalCode = postal;
        Phone = Guard.Optional(phone, "Telefon", 60);
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}

public interface IPrisonRepository
{
    Task<Prison?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>İlin aktif kurumları, ada göre sıralı.</summary>
    Task<IReadOnlyList<Prison>> ListActiveByCityAsync(string city, CancellationToken ct = default);
    /// <summary>Yönetim listesi: il ve arama metnine göre (ikisi de isteğe bağlı).</summary>
    Task<IReadOnlyList<Prison>> SearchAsync(string? city, string? query, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    void Add(Prison prison);
    void AddRange(IEnumerable<Prison> prisons);
    void Remove(Prison prison);
}
