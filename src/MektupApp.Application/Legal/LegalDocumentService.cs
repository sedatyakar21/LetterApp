using MektupApp.Application.Abstractions;
using MektupApp.Domain.Common;
using MektupApp.Domain.Legal;

namespace MektupApp.Application.Legal;

public sealed record LegalDocumentSummaryDto(Guid Id, string Slug, string Title, bool IsPublished, bool ShowInFooter, int SortOrder,
    bool IsSystem, int Version, DateTime UpdatedAt, string? UpdatedBy);

public sealed record LegalDocumentDto(Guid Id, string Slug, string Title, string ContentMarkdown, bool IsPublished, bool ShowInFooter,
    int SortOrder, bool IsSystem, int Version, DateTime UpdatedAt, string? UpdatedBy);

public sealed record SaveLegalDocumentCommand(string Title, string? Slug, string ContentMarkdown, bool ShowInFooter, int SortOrder);

/// <summary>Sözleşmeler ve yasal metinler: yönetim panelinden düzenlenir, sitede /sozlesme/{adres} olarak yayınlanır.</summary>
public sealed class LegalDocumentService(ILegalDocumentRepository documents, IUnitOfWork unitOfWork)
{
    // ---------------- site ----------------

    /// <summary>Yayındaki belge (sitedeki sayfa); yoksa veya yayında değilse null.</summary>
    public async Task<LegalDocumentDto?> GetPublishedAsync(string slug, bool includeDrafts = false, CancellationToken ct = default)
    {
        var d = await documents.GetBySlugAsync((slug ?? "").Trim().ToLowerInvariant(), ct);
        return d is not null && (d.IsPublished || includeDrafts) ? ToDto(d) : null;
    }

    /// <summary>Alt bilgide ve uygulamada listelenecek yayındaki belgeler (sırasıyla).</summary>
    public async Task<IReadOnlyList<LegalDocumentSummaryDto>> ListPublishedAsync(bool footerOnly, CancellationToken ct = default) =>
        (await documents.ListAsync(ct)).Where(d => d.IsPublished && (!footerOnly || d.ShowInFooter)).Select(ToSummary).ToList();

    // ---------------- yönetim ----------------

    public async Task<IReadOnlyList<LegalDocumentSummaryDto>> ListAllAsync(CancellationToken ct = default) =>
        (await documents.ListAsync(ct)).Select(ToSummary).ToList();

    public async Task<LegalDocumentDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await LoadAsync(id, ct));

    public async Task<Guid> CreateAsync(SaveLegalDocumentCommand cmd, bool publish, string updatedBy, CancellationToken ct = default)
    {
        var d = LegalDocument.Create(cmd.Title, cmd.Slug, cmd.ContentMarkdown, cmd.ShowInFooter, cmd.SortOrder, updatedBy);
        await EnsureUniqueSlugAsync(d.Slug, null, ct);
        if (publish) d.Publish();
        documents.Add(d);
        await unitOfWork.SaveChangesAsync(ct);
        return d.Id;
    }

    public async Task UpdateAsync(Guid id, SaveLegalDocumentCommand cmd, bool publish, string updatedBy, CancellationToken ct = default)
    {
        var d = await LoadAsync(id, ct);
        d.Update(cmd.Title, cmd.Slug, cmd.ContentMarkdown, cmd.ShowInFooter, cmd.SortOrder, updatedBy);
        await EnsureUniqueSlugAsync(d.Slug, d.Id, ct);
        if (publish) d.Publish();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetPublishedAsync(Guid id, bool published, CancellationToken ct = default)
    {
        var d = await LoadAsync(id, ct);
        if (published) d.Publish(); else d.Unpublish();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var d = await LoadAsync(id, ct);
        d.EnsureDeletable();
        documents.Remove(d);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sitede bağlantısı olan sistem belgeleri yoksa taslak iskeletleriyle oluşturulur (açılışta çağrılır).
    /// Mevcut belgelere dokunulmaz; metinler yönetim panelinden girilir.
    /// </summary>
    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var existing = (await documents.ListAsync(ct)).Select(d => d.Slug).ToHashSet();
        var added = false;
        foreach (var (slug, title, order, footer) in Defaults)
        {
            if (existing.Contains(slug)) continue;
            var d = LegalDocument.Create(title, slug, DraftContent(title), footer, order, "sistem", isSystem: true);
            d.Publish();
            documents.Add(d);
            added = true;
        }
        if (added) await unitOfWork.SaveChangesAsync(ct);
    }

    private static readonly (string Slug, string Title, int Order, bool Footer)[] Defaults =
    [
        (LegalDocument.MembershipSlug, "Üyelik Sözleşmesi", 10, true),
        (LegalDocument.DistanceSalesSlug, "Mesafeli Satış Sözleşmesi", 20, true),
        (LegalDocument.PreInformationSlug, "Ön Bilgilendirme Formu", 30, false),
        (LegalDocument.CancellationSlug, "İptal ve İade Koşulları", 40, true),
        (LegalDocument.PrivacyNoticeSlug, "KVKK Aydınlatma Metni", 50, true),
        (LegalDocument.PrivacyPolicySlug, "Gizlilik ve Çerez Politikası", 60, true)
    ];

    private static string DraftContent(string title) =>
        $"""
        > **Taslak metin.** Bu "{title}" için yer tutucudur. Yayına almadan önce hukuk danışmanınızla hazırlanmış metni
        > yönetim panelindeki **Sözleşmeler** ekranından girin.
        """;

    private async Task EnsureUniqueSlugAsync(string slug, Guid? exceptId, CancellationToken ct)
    {
        if (await documents.SlugExistsAsync(slug, exceptId, ct))
            throw new DomainException($"\"{slug}\" adresi başka bir belgede kullanılıyor. Farklı bir adres girin.");
    }

    private async Task<LegalDocument> LoadAsync(Guid id, CancellationToken ct) =>
        await documents.GetAsync(id, ct) ?? throw new AppException("Belge bulunamadı.");

    private static LegalDocumentSummaryDto ToSummary(LegalDocument d) =>
        new(d.Id, d.Slug, d.Title, d.IsPublished, d.ShowInFooter, d.SortOrder, d.IsSystem, d.Version, d.UpdatedAt, d.UpdatedBy);

    private static LegalDocumentDto ToDto(LegalDocument d) =>
        new(d.Id, d.Slug, d.Title, d.ContentMarkdown, d.IsPublished, d.ShowInFooter, d.SortOrder, d.IsSystem, d.Version, d.UpdatedAt, d.UpdatedBy);
}
