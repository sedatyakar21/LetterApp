using MektupApp.Application.Abstractions;
using MektupApp.Application.Campaigns;
using MektupApp.Application.Customers;
using MektupApp.Application.Letters;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Application.Reviews;
using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MektupApp.Web.Api;

public sealed record OptionDto(string Value, string Label);

public sealed record SupportDto(string? WhatsAppNumber, string? WhatsAppDisplay, string? Email, string? Hours);

public sealed record MobileConfigDto(
    PriceList Prices,
    IReadOnlyList<OptionDto> RecipientTypes,
    IReadOnlyList<OptionDto> PaperColors,
    IReadOnlyList<OptionDto> EnvelopeColors,
    IReadOnlyList<OptionDto> Fonts,
    IReadOnlyList<OptionDto> Inks,
    IReadOnlyList<OptionDto> Scents,
    IReadOnlyList<OptionDto> PostcardCategories,
    IReadOnlyList<OptionDto> AcquisitionChannels,
    int PostcardDesignsPerCategory,
    int MaxPhotos,
    long MaxPhotoBytes,
    int MaxContentLength,
    int CharsPerPage,
    int CharsPerLineBreak,
    int MinPasswordLength,
    bool LoyaltyEnabled,
    bool MobilePaymentAvailable,
    bool PaymentSimulation,
    SupportDto Support);

public sealed record CampaignCardApiDto(
    Guid Id, string Title, string Slug, string Summary, string? BadgeText, string? CouponCode,
    string Phase, string StatusText, string DateRange, string? AudienceText, string? CoverUrl);

public sealed record CampaignDetailApiDto(
    Guid Id, string Title, string Slug, string Summary, string Description, string? BadgeText, string? CouponCode,
    string Phase, string StatusText, string DateRange, string? AudienceText, string? CoverUrl,
    decimal? MinOrderAmount);

public sealed record ReviewApiDto(Guid Id, string AuthorName, string? RecipientCity, int Rating, string Comment, DateTime CreatedAt);

public sealed record ReviewListDto(double Average, int Count, IReadOnlyList<ReviewApiDto> Items, int Page, bool HasNext);

public sealed record StatusChangeApiDto(string Status, string StatusLabel, string Note, DateTime At);

public sealed record TrackingApiDto(string TrackingCode, string RecipientInitials, string City, string Status, string StatusLabel,
    IReadOnlyList<StatusChangeApiDto> History);

/// <summary>Giriş gerektirmeyen içerik: uygulama ayarları, kampanyalar, yorumlar ve gönderi takibi.</summary>
[Route("api/mobile")]
[AllowAnonymous]
public sealed class MobileContentController(
    PricingService pricing,
    LoyaltyService loyalty,
    CampaignService campaigns,
    ReviewService reviews,
    LetterService letters,
    IPaymentProvider paymentProvider,
    IOptions<SupportOptions> support) : MobileApiController
{
    /// <summary>Formdaki seçenekler, fiyatlar ve sınırlar. Uygulama açılışta bir kez okur.</summary>
    [HttpGet("config")]
    public async Task<MobileConfigDto> Config(CancellationToken ct)
    {
        var s = support.Value;
        return new MobileConfigDto(
            await pricing.GetCurrentAsync(ct),
            Options<RecipientType>(), Options<PaperColor>(), Options<EnvelopeColor>(), Options<LetterFont>(),
            Options<InkColor>(), Options<Scent>(), Options<PostcardCategory>(), Options<AcquisitionChannel>(),
            Postcard.DesignsPerCategory, Letter.MaxPhotos, LetterService.MaxPhotoBytes, Letter.MaxContentLength,
            LetterPages.CharsPerPage, LetterPages.CharsPerLineBreak, AccountService.MinPasswordLength,
            (await loyalty.GetSettingsAsync(ct)).IsEnabled,
            paymentProvider.SupportsMobilePayment, paymentProvider.IsSimulation,
            new SupportDto(s.WhatsAppEnabled ? s.Digits : null, s.WhatsAppDisplay, s.Email, s.Hours));
    }

    [HttpGet("campaigns")]
    public async Task<IReadOnlyList<CampaignCardApiDto>> Campaigns(CancellationToken ct) =>
        (await campaigns.ListCurrentAsync(ct)).Select(c => new CampaignCardApiDto(
            c.Id, c.Title, c.Slug, c.Summary, c.BadgeText, c.CouponCode, c.Phase.ToString(),
            CampaignText.Status(c.Phase, c.DaysLeft, c.StartDate), CampaignText.DateRange(c.StartDate, c.EndDate),
            AudienceText(c.Audience, c.AudienceDays, c.UsageLimitPerCustomer), CoverUrl(c.Id, c.HasCover))).ToList();

    [HttpGet("campaigns/{slug}")]
    public async Task<ActionResult<CampaignDetailApiDto>> Campaign(string slug, CancellationToken ct)
    {
        var c = await campaigns.GetBySlugAsync(slug, includeDrafts: false, ct);
        if (c is null) return NotFound(new ApiError("Kampanya bulunamadı."));
        return new CampaignDetailApiDto(c.Id, c.Title, c.Slug, c.Summary, c.DescriptionMarkdown, c.BadgeText, c.CouponCode,
            c.Phase.ToString(), CampaignText.Status(c.Phase, c.DaysLeft, c.StartDate), CampaignText.DateRange(c.StartDate, c.EndDate),
            AudienceText(c.Audience, c.AudienceDays, c.UsageLimitPerCustomer), CoverUrl(c.Id, c.HasCover),
            c.DiscountType == CampaignDiscountType.None ? null : c.MinOrderAmount);
    }

    [HttpGet("reviews")]
    public async Task<ReviewListDto> Reviews(int page = 1, CancellationToken ct = default)
    {
        var summary = await reviews.GetSummaryAsync(ct);
        var list = await reviews.ListApprovedAsync(page, 20, ct);
        return new ReviewListDto(summary.Average, summary.Count,
            list.Items.Select(r => new ReviewApiDto(r.Id, r.AuthorName, r.RecipientCity, r.Rating, r.Comment, r.CreatedAt)).ToList(),
            list.Page, list.HasNext);
    }

    [HttpGet("tracking/{code}")]
    public async Task<ActionResult<TrackingApiDto>> Track(string code, CancellationToken ct)
    {
        var r = await letters.TrackAsync(code, ct);
        if (r is null) return NotFound(new ApiError("Bu takip numarasıyla bir gönderi bulunamadı."));
        return new TrackingApiDto(r.TrackingCode, r.RecipientInitials, r.City, r.Status.ToString(), r.Status.DisplayName(),
            r.History.Select(ApiMapping.ToApi).ToList());
    }

    private static IReadOnlyList<OptionDto> Options<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new OptionDto(v.ToString(), v.DisplayName())).ToList();

    private static string? AudienceText(CampaignAudience audience, int? days, int? limit) =>
        audience == CampaignAudience.Everyone && limit is null ? null : CampaignText.Audience(audience, days, limit);

    private string? CoverUrl(Guid id, bool hasCover) =>
        hasCover ? Url.Action("Cover", "Campaigns", new { id }, Request.Scheme) : null;
}

public static class ApiMapping
{
    public static StatusChangeApiDto ToApi(StatusChangeDto h) => new(h.Status.ToString(), h.Status.DisplayName(), h.Note, h.At);
}
