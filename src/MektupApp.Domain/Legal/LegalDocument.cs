using MektupApp.Domain.Common;

namespace MektupApp.Domain.Legal;

/// <summary>
/// Sözleşme / yasal metin (üyelik sözleşmesi, mesafeli satış sözleşmesi, KVKK aydınlatma metni vb.).
/// Yönetim panelinden Markdown olarak girilir ve /sozlesme/{adres} sayfasında yayınlanır.
/// Sistem belgelerinin (sitede bağlantısı olanlar) adresi sabittir ve silinemez; içerik değiştikçe sürüm artar.
/// </summary>
public sealed class LegalDocument : AggregateRoot
{
    public const int TitleMax = 150;
    public const int SlugMax = 80;
    public const int ContentMax = 200_000;

    // Sitede / uygulamada bağlantısı bulunan sistem belgeleri.
    public const string MembershipSlug = "uyelik-sozlesmesi";
    public const string DistanceSalesSlug = "mesafeli-satis-sozlesmesi";
    public const string PreInformationSlug = "on-bilgilendirme-formu";
    public const string PrivacyNoticeSlug = "kvkk-aydinlatma-metni";
    public const string PrivacyPolicySlug = "gizlilik-ve-cerez-politikasi";
    public const string CancellationSlug = "iptal-ve-iade-kosullari";

    private LegalDocument() { } // EF Core

    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string ContentMarkdown { get; private set; } = string.Empty;
    public bool IsPublished { get; private set; }
    /// <summary>Sitenin alt bilgisinde bağlantısı gösterilsin mi.</summary>
    public bool ShowInFooter { get; private set; }
    public int SortOrder { get; private set; }
    /// <summary>Sistem belgesi: adresi değiştirilemez, silinemez.</summary>
    public bool IsSystem { get; private set; }
    /// <summary>İçerik her değiştiğinde artar (kabul edilen sürümü izlemek için).</summary>
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static LegalDocument Create(string title, string? slug, string contentMarkdown, bool showInFooter, int sortOrder,
        string updatedBy, bool isSystem = false)
    {
        var now = DateTime.UtcNow;
        var d = new LegalDocument { CreatedAt = now, IsSystem = isSystem, Version = 1 };
        d.Slug = NormalizeSlug(string.IsNullOrWhiteSpace(slug) ? title : slug);
        d.Apply(title, contentMarkdown, showInFooter, sortOrder, updatedBy, now);
        return d;
    }

    /// <summary>Başlık, içerik ve görünüm ayarları. İçerik değiştiyse sürüm artar.</summary>
    public void Update(string title, string? slug, string contentMarkdown, bool showInFooter, int sortOrder, string updatedBy)
    {
        var newSlug = NormalizeSlug(string.IsNullOrWhiteSpace(slug) ? Slug : slug);
        if (IsSystem && newSlug != Slug)
            throw new DomainException("Bu belgenin adresi sitede ve uygulamada kullanıldığı için değiştirilemez.");
        var contentChanged = (contentMarkdown ?? string.Empty).Trim() != ContentMarkdown;
        Slug = newSlug;
        Apply(title, contentMarkdown ?? string.Empty, showInFooter, sortOrder, updatedBy, DateTime.UtcNow);
        if (contentChanged) Version++;
    }

    public void Publish()
    {
        if (string.IsNullOrWhiteSpace(ContentMarkdown)) throw new DomainException("Boş bir belge yayınlanamaz. Önce metni girin.");
        IsPublished = true;
    }

    public void Unpublish() => IsPublished = false;

    public void EnsureDeletable()
    {
        if (IsSystem) throw new DomainException("Bu belge sitede kullanıldığı için silinemez; yayından kaldırabilirsiniz.");
    }

    private void Apply(string title, string contentMarkdown, bool showInFooter, int sortOrder, string updatedBy, DateTime now)
    {
        Title = Guard.Required(title, "Başlık", TitleMax);
        var content = (contentMarkdown ?? string.Empty).Trim();
        if (content.Length > ContentMax) throw new DomainException($"Metin en fazla {ContentMax:N0} karakter olabilir.");
        if (IsPublished && content.Length == 0) throw new DomainException("Yayındaki bir belgenin metni boş bırakılamaz.");
        ContentMarkdown = content;
        ShowInFooter = showInFooter;
        SortOrder = sortOrder;
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = now;
    }

    private static string NormalizeSlug(string? value)
    {
        var s = Common.Slug.From(value);
        if (s.Length == 0) throw new DomainException("Geçerli bir adres girin (harf ve rakam).");
        return s.Length > SlugMax ? s[..SlugMax].TrimEnd('-') : s;
    }
}

public interface ILegalDocumentRepository
{
    Task<LegalDocument?> GetAsync(Guid id, CancellationToken ct = default);
    Task<LegalDocument?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    Task<IReadOnlyList<LegalDocument>> ListAsync(CancellationToken ct = default);
    void Add(LegalDocument document);
    void Remove(LegalDocument document);
}
