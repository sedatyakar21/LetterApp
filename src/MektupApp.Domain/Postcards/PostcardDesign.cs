using MektupApp.Domain.Common;

namespace MektupApp.Domain.Postcards;

/// <summary>
/// Kartpostal kataloğundaki bir kartpostal (yönetim panelinden eklenir). Müşteri mektubuna eklerken bunlardan birini seçer;
/// mektupta başlık ve kategori anlık kopya olarak saklanır, böylece katalog sonradan değişse de sipariş bilgisi bozulmaz.
/// </summary>
public sealed class PostcardDesign : AggregateRoot
{
    public const int TitleMax = 100;
    public const int CategoryMax = 50;

    private PostcardDesign() { } // EF Core

    public string Title { get; private set; } = string.Empty;
    /// <summary>Serbest metin (ör. "Aşk", "Bayram"); müşteri ekranında kartpostallar bu başlıkla gruplanır.</summary>
    public string Category { get; private set; } = string.Empty;
    /// <summary>Kartpostalın ön yüz görseli (dosya deposundaki anahtar).</summary>
    public string ImageKey { get; private set; } = string.Empty;
    /// <summary>Pasif kartpostal müşteriye gösterilmez (stok bitti vb.); onu seçmiş mektuplar etkilenmez.</summary>
    public bool IsActive { get; private set; }
    /// <summary>Küçük sayı önce gösterilir.</summary>
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static PostcardDesign Create(string title, string category, string imageKey, int sortOrder = 0, bool isActive = true)
    {
        var p = new PostcardDesign
        {
            ImageKey = Guard.Required(imageKey, "Görsel", 300),
            CreatedAt = DateTime.UtcNow
        };
        p.Update(title, category, sortOrder, isActive);
        return p;
    }

    public void Update(string title, string category, int sortOrder, bool isActive)
    {
        Title = Guard.Required(title, "Kartpostal adı", TitleMax);
        Category = NormalizeCategory(category);
        if (sortOrder is < 0 or > 9999) throw new DomainException("Sıra 0 ile 9999 arasında olmalıdır.");
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Görseli değiştirir; silinmesi gereken eski görselin anahtarını döner.</summary>
    public string ReplaceImage(string imageKey)
    {
        var old = ImageKey;
        ImageKey = Guard.Required(imageKey, "Görsel", 300);
        UpdatedAt = DateTime.UtcNow;
        return old;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Fazla boşluklar temizlenir; "aşk" ile "Aşk" ayrı kategori olmasın diye ilk harf büyütülür.</summary>
    public static string NormalizeCategory(string? category)
    {
        var c = string.Join(' ', (Guard.Required(category, "Kategori", CategoryMax)).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        return char.ToUpper(c[0], tr) + c[1..];
    }
}

public interface IPostcardRepository
{
    Task<PostcardDesign?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sıraya, sonra kategori ve ada göre.</summary>
    Task<IReadOnlyList<PostcardDesign>> ListAsync(bool activeOnly, CancellationToken ct = default);
    /// <summary>Bu kartpostalı seçmiş (silinmemiş) mektup var mı.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default);
    void Add(PostcardDesign postcard);
    void Remove(PostcardDesign postcard);
}
