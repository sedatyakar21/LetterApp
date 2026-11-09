using MektupApp.Application.Abstractions;
using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Common;

namespace MektupApp.Application.Campaigns;

public sealed record CampaignCardDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string? BadgeText,
    string? CouponCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool HasCover,
    CampaignPhase Phase,
    int? DaysLeft,
    CampaignDiscountType DiscountType = CampaignDiscountType.None,
    CampaignAudience Audience = CampaignAudience.Everyone,
    int? AudienceDays = null,
    int? UsageLimitPerCustomer = null);

public sealed record CampaignDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string DescriptionMarkdown,
    string? BadgeText,
    string? CouponCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool HasCover,
    bool IsPublished,
    CampaignPhase Phase,
    int? DaysLeft,
    DateTime UpdatedAt,
    CampaignDiscountType DiscountType,
    decimal DiscountValue,
    decimal? MinOrderAmount,
    CampaignAudience Audience,
    int? AudienceDays,
    int? UsageLimitPerCustomer);

public sealed record SaveCampaignCommand(
    string Title,
    string? Slug,
    string Summary,
    string? DescriptionMarkdown,
    string? BadgeText,
    string? CouponCode,
    DateOnly StartDate,
    DateOnly? EndDate,
    CampaignDiscountType DiscountType = CampaignDiscountType.None,
    decimal DiscountValue = 0,
    decimal? MinOrderAmount = null,
    CampaignAudience Audience = CampaignAudience.Everyone,
    int? AudienceDays = null,
    int? UsageLimitPerCustomer = null);

/// <summary>"Bugün" Türkiye takvimine göre: kampanya günleri gece yarısı TR saatiyle değişir.</summary>
public static class TurkeyCalendar
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));
}

/// <summary>Kampanyalar: ziyaretçi sayfaları ve yönetim. İndirimin ödemede uygulanması: <see cref="Payments.CheckoutCalculator"/>.</summary>
public sealed class CampaignService(ICampaignRepository campaigns, IFileStorage files, IUnitOfWork unitOfWork)
{
    private static readonly HashSet<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public const long MaxCoverBytes = 4 * 1024 * 1024;

    // ---------------- Ziyaretçi ----------------

    /// <summary>Devam eden (önce bitişi yakın olanlar) ve yakında başlayacak kampanyalar.</summary>
    public async Task<IReadOnlyList<CampaignCardDto>> ListCurrentAsync(CancellationToken ct = default)
    {
        var today = TurkeyCalendar.Today;
        return (await campaigns.ListCurrentAsync(today, ct))
            .Select(c => ToCard(c, today))
            .OrderBy(c => c.Phase == CampaignPhase.Active ? 0 : 1)
            .ThenBy(c => c.Phase == CampaignPhase.Active ? (c.EndDate ?? DateOnly.MaxValue) : c.StartDate)
            .ToList();
    }

    public async Task<IReadOnlyList<CampaignCardDto>> ListActiveAsync(CancellationToken ct = default) =>
        (await ListCurrentAsync(ct)).Where(c => c.Phase == CampaignPhase.Active).ToList();

    public async Task<IReadOnlyList<CampaignCardDto>> ListEndedAsync(int maxCount, CancellationToken ct = default)
    {
        var today = TurkeyCalendar.Today;
        return (await campaigns.ListEndedAsync(today, maxCount, ct)).Select(c => ToCard(c, today)).ToList();
    }

    /// <param name="includeDrafts">Yönetici önizlemesi için true.</param>
    public async Task<CampaignDto?> GetBySlugAsync(string slug, bool includeDrafts, CancellationToken ct = default)
    {
        var c = await campaigns.GetBySlugAsync(Slug.From(slug), ct);
        return c is null || (!c.IsPublished && !includeDrafts) ? null : ToDto(c, TurkeyCalendar.Today);
    }

    public async Task<(Stream Stream, string Key)?> OpenCoverAsync(Guid id, bool includeDrafts, CancellationToken ct = default)
    {
        var c = await campaigns.GetAsync(id, ct);
        if (c?.CoverImageKey is null || (!c.IsPublished && !includeDrafts)) return null;
        var stream = await files.OpenAsync(c.CoverImageKey, ct);
        return stream is null ? null : (stream, c.CoverImageKey);
    }

    // ---------------- Yönetim ----------------

    public async Task<IReadOnlyList<CampaignCardDto>> ListAllAsync(CancellationToken ct = default)
    {
        var today = TurkeyCalendar.Today;
        return (await campaigns.ListAllAsync(ct)).Select(c => ToCard(c, today)).ToList();
    }

    public async Task<CampaignDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await LoadAsync(id, ct), TurkeyCalendar.Today);

    public async Task<Guid> CreateAsync(SaveCampaignCommand cmd, bool publish, CancellationToken ct = default)
    {
        var c = Campaign.Create(cmd.Title, cmd.Summary, cmd.DescriptionMarkdown ?? string.Empty, cmd.StartDate, cmd.EndDate,
            cmd.BadgeText, cmd.CouponCode, cmd.Slug);
        c.SetDiscount(cmd.DiscountType, cmd.DiscountValue, cmd.MinOrderAmount);
        c.SetAudience(cmd.Audience, cmd.AudienceDays, cmd.UsageLimitPerCustomer);
        await EnsureUniqueSlugAsync(c, ct);
        if (publish) c.Publish();
        campaigns.Add(c);
        await unitOfWork.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task UpdateAsync(Guid id, SaveCampaignCommand cmd, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, ct);
        c.Edit(cmd.Title, cmd.Summary, cmd.DescriptionMarkdown ?? string.Empty, cmd.StartDate, cmd.EndDate, cmd.BadgeText, cmd.CouponCode);
        c.SetDiscount(cmd.DiscountType, cmd.DiscountValue, cmd.MinOrderAmount);
        c.SetAudience(cmd.Audience, cmd.AudienceDays, cmd.UsageLimitPerCustomer);
        if (!string.IsNullOrWhiteSpace(cmd.Slug) && Slug.From(cmd.Slug) != c.Slug)
        {
            c.ChangeSlug(cmd.Slug);
            await EnsureUniqueSlugAsync(c, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task PublishAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, c => c.Publish(), ct);

    public Task UnpublishAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, c => c.Unpublish(), ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, ct);
        var cover = c.CoverImageKey;
        campaigns.Remove(c);
        await unitOfWork.SaveChangesAsync(ct);
        if (cover is not null) await files.DeleteAsync(cover, ct);
    }

    public async Task SetCoverAsync(Guid id, Stream content, string fileName, long length, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            throw new AppException("Kapak görseli JPG, PNG veya WEBP olmalıdır.");
        if (length is <= 0 or > MaxCoverBytes)
            throw new AppException("Kapak görseli en fazla 4 MB olabilir.");

        var c = await LoadAsync(id, ct);
        var key = await files.SaveAsync(content, ext, ct);
        var old = c.SetCover(key);
        await unitOfWork.SaveChangesAsync(ct);
        if (old is not null) await files.DeleteAsync(old, ct);
    }

    public async Task RemoveCoverAsync(Guid id, CancellationToken ct = default)
    {
        var c = await LoadAsync(id, ct);
        var old = c.SetCover(null);
        await unitOfWork.SaveChangesAsync(ct);
        if (old is not null) await files.DeleteAsync(old, ct);
    }

    // ---------------- yardımcılar ----------------

    private async Task EnsureUniqueSlugAsync(Campaign c, CancellationToken ct)
    {
        var baseSlug = c.Slug;
        for (var n = 2; await campaigns.SlugExistsAsync(c.Slug, c.Id, ct); n++)
            c.ChangeSlug(Slug.WithSuffix(baseSlug, n));
    }

    private async Task ExecuteAsync(Guid id, Action<Campaign> action, CancellationToken ct)
    {
        action(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Campaign> LoadAsync(Guid id, CancellationToken ct) =>
        await campaigns.GetAsync(id, ct) ?? throw new AppException("Kampanya bulunamadı.");

    private static CampaignCardDto ToCard(Campaign c, DateOnly today) =>
        new(c.Id, c.Title, c.Slug, c.Summary, c.BadgeText, c.CouponCode, c.StartDate, c.EndDate,
            c.CoverImageKey is not null, c.PhaseOn(today), c.DaysLeftOn(today), c.DiscountType,
            c.Audience, c.AudienceDays, c.UsageLimitPerCustomer);

    private static CampaignDto ToDto(Campaign c, DateOnly today) =>
        new(c.Id, c.Title, c.Slug, c.Summary, c.DescriptionMarkdown, c.BadgeText, c.CouponCode, c.StartDate, c.EndDate,
            c.CoverImageKey is not null, c.IsPublished, c.PhaseOn(today), c.DaysLeftOn(today), c.UpdatedAt,
            c.DiscountType, c.DiscountValue, c.MinOrderAmount, c.Audience, c.AudienceDays, c.UsageLimitPerCustomer);
}
