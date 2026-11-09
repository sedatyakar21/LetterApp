using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Campaigns;

/// <summary>Kampanyanın belirli bir güne göre durumu (tarihlerden hesaplanır, saklanmaz).</summary>
public enum CampaignPhase
{
    [Display(Name = "Taslak")] Draft = 0,
    [Display(Name = "Yakında")] Upcoming = 1,
    [Display(Name = "Devam ediyor")] Active = 2,
    [Display(Name = "Sona erdi")] Ended = 3
}

/// <summary>Kampanyanın ödeme ekranında uyguladığı indirim türü.</summary>
public enum CampaignDiscountType
{
    [Display(Name = "Sadece duyuru (indirim yok)")] None = 0,
    [Display(Name = "Yüzde indirim")] Percentage = 1,
    [Display(Name = "Sabit tutar indirim (₺)")] FixedAmount = 2,
    [Display(Name = "Ücretsiz koku")] FreeScent = 3,
    [Display(Name = "Ücretsiz kartpostal")] FreePostcard = 4
}

/// <summary>Kampanyadan kimlerin yararlanabileceği.</summary>
public enum CampaignAudience
{
    [Display(Name = "Herkes")] Everyone = 0,
    [Display(Name = "Yeni üyeler")] NewMembers = 1,
    [Display(Name = "Doğum günü olanlar")] Birthday = 2
}

/// <summary>Müşterinin bir kampanyayı kullandığı gün (ödenmiş veya ödemesi süren sipariş).</summary>
public sealed record CampaignUse(Guid CampaignId, DateOnly UsedOn);

/// <summary>Kampanyaya uygunluk için bakılan müşteri bilgisi.</summary>
public sealed record CampaignCustomer(DateOnly MemberSince, DateOnly? BirthDate, IReadOnlyList<CampaignUse> Uses);

/// <summary>Kampanya indirimi hesaplanırken bakılan sepet bilgisi (puan kullanımı öncesi).</summary>
/// <param name="Total">Fiyat listesine göre toplam tutar.</param>
/// <param name="ScentAmount">Sepetteki koku ücreti (koku seçilmediyse 0).</param>
/// <param name="PostcardAmount">Sepetteki kartpostal ücreti (kartpostal yoksa 0).</param>
public sealed record CampaignCart(decimal Total, decimal ScentAmount, decimal PostcardAmount);

/// <summary>
/// Kampanya (aggregate root). İndirim türü "Sadece duyuru" ise fiyatı etkilemez; aksi halde ödeme ekranında uygulanır.
/// Kupon kodu olan kampanya yalnızca kod girildiğinde, kodu olmayan herkese otomatik uygulanır.
/// Tarihler Türkiye takvimine göre gün olarak tutulur; bitiş günü dahildir. Bitiş yoksa süresizdir.
/// </summary>
public sealed class Campaign : AggregateRoot
{
    public const int TitleMax = 120;
    public const int SummaryMax = 300;
    public const int BadgeMax = 30;
    public const int CouponMax = 30;
    public const int DescriptionMax = 50_000;

    private Campaign() { } // EF Core

    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    /// <summary>Kartta ve arama motoru açıklamasında görünen kısa metin.</summary>
    public string Summary { get; private set; } = string.Empty;
    public string DescriptionMarkdown { get; private set; } = string.Empty;
    /// <summary>Kartın köşesindeki kısa etiket (ör. "%20 İndirim", "Ücretsiz Koku").</summary>
    public string? BadgeText { get; private set; }
    /// <summary>Kupon kodu. Doluysa indirim yalnızca bu kod ödeme ekranında girildiğinde uygulanır.</summary>
    public string? CouponCode { get; private set; }
    public CampaignDiscountType DiscountType { get; private set; }
    /// <summary>Yüzde (1–100) veya TL tutarı. Ücretsiz koku / kartpostal ve duyuruda 0.</summary>
    public decimal DiscountValue { get; private set; }
    /// <summary>İndirimin geçerli olması için gereken en düşük sepet tutarı (yoksa null).</summary>
    public decimal? MinOrderAmount { get; private set; }
    public CampaignAudience Audience { get; private set; }
    /// <summary>Yeni üyeler: üyelikten sonraki gün sayısı. Doğum günü: doğum gününden önce ve sonra kaç gün geçerli.</summary>
    public int? AudienceDays { get; private set; }
    /// <summary>Üye başına en fazla kullanım (doğum günü kampanyasında her doğum günü için ayrı sayılır). Boşsa sınırsız.</summary>
    public int? UsageLimitPerCustomer { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? CoverImageKey { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Campaign Create(string title, string summary, string descriptionMarkdown, DateOnly startDate, DateOnly? endDate,
        string? badgeText = null, string? couponCode = null, string? slug = null)
    {
        var c = new Campaign { CreatedAt = DateTime.UtcNow };
        c.Edit(title, summary, descriptionMarkdown, startDate, endDate, badgeText, couponCode);
        c.ChangeSlug(string.IsNullOrWhiteSpace(slug) ? title : slug);
        return c;
    }

    public void Edit(string title, string summary, string descriptionMarkdown, DateOnly startDate, DateOnly? endDate,
        string? badgeText, string? couponCode)
    {
        if (endDate is { } end && end < startDate)
            throw new DomainException("Bitiş tarihi başlangıç tarihinden önce olamaz.");

        Title = Guard.Required(title, "Başlık", TitleMax);
        Summary = Guard.Required(summary, "Kısa açıklama", SummaryMax);
        DescriptionMarkdown = Guard.Optional(descriptionMarkdown, "Açıklama", DescriptionMax) ?? string.Empty;
        BadgeText = Guard.Optional(badgeText, "Rozet", BadgeMax);
        CouponCode = Guard.Optional(couponCode, "Kupon kodu", CouponMax)?.ToUpperInvariant();
        StartDate = startDate;
        EndDate = endDate;
        Touch();
    }

    public void ChangeSlug(string slugOrTitle)
    {
        var s = Common.Slug.From(slugOrTitle);
        if (s.Length == 0) throw new DomainException("Kampanya adresi en az bir harf veya rakam içermelidir.");
        Slug = s;
        Touch();
    }

    public void SetDiscount(CampaignDiscountType type, decimal value, decimal? minOrderAmount)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Geçersiz indirim türü.");
        switch (type)
        {
            case CampaignDiscountType.Percentage when value is <= 0 or > 100:
                throw new DomainException("İndirim yüzdesi 0'dan büyük, en fazla 100 olmalıdır.");
            case CampaignDiscountType.FixedAmount when value <= 0:
                throw new DomainException("İndirim tutarı 0'dan büyük olmalıdır.");
        }
        if (minOrderAmount < 0) throw new DomainException("En düşük sepet tutarı negatif olamaz.");

        DiscountType = type;
        DiscountValue = type is CampaignDiscountType.Percentage or CampaignDiscountType.FixedAmount ? decimal.Round(value, 2) : 0;
        MinOrderAmount = type == CampaignDiscountType.None || minOrderAmount is null or 0 ? null : decimal.Round(minOrderAmount.Value, 2);
        Touch();
    }

    public void SetAudience(CampaignAudience audience, int? days, int? usageLimitPerCustomer)
    {
        if (!Enum.IsDefined(audience)) throw new DomainException("Geçersiz hedef kitle.");
        switch (audience)
        {
            case CampaignAudience.NewMembers when days is not (>= 1 and <= 365):
                throw new DomainException("Yeni üye kampanyası için üyelikten sonraki gün sayısı 1 ile 365 arasında olmalıdır.");
            case CampaignAudience.Birthday when days is not (>= 0 and <= 30):
                throw new DomainException("Doğum günü kampanyası için gün aralığı 0 ile 30 arasında olmalıdır.");
        }
        if (usageLimitPerCustomer is not null and not (>= 1 and <= 100))
            throw new DomainException("Üye başına kullanım sınırı 1 ile 100 arasında olmalıdır.");

        Audience = audience;
        AudienceDays = audience == CampaignAudience.Everyone ? null : days;
        UsageLimitPerCustomer = usageLimitPerCustomer;
        Touch();
    }

    /// <summary>Bu müşteri bugün kampanyadan yararlanabilir mi (hedef kitle ve kullanım sınırı). Tarih / indirim kontrolü ayrıdır.</summary>
    public bool IsEligible(CampaignCustomer? customer, DateOnly today)
    {
        if (Audience == CampaignAudience.Everyone && UsageLimitPerCustomer is null) return true;
        if (customer is null) return false;

        var countUsesFrom = DateOnly.MinValue;
        switch (Audience)
        {
            case CampaignAudience.NewMembers:
                var age = today.DayNumber - customer.MemberSince.DayNumber;
                if (age < 0 || age > AudienceDays) return false;
                break;
            case CampaignAudience.Birthday:
                if (customer.BirthDate is not { } birth || BirthdayWindowStart(birth, today, AudienceDays ?? 0) is not { } start)
                    return false;
                countUsesFrom = start; // her doğum günü için kullanım hakkı yenilenir
                break;
        }

        return UsageLimitPerCustomer is not { } limit
               || customer.Uses.Count(u => u.CampaignId == Id && u.UsedOn >= countUsesFrom) < limit;
    }

    /// <summary>Bugün doğum günü penceresindeyse pencerenin ilk günü, değilse null. Yılbaşı geçişini ve 29 Şubat'ı hesaba katar.</summary>
    public static DateOnly? BirthdayWindowStart(DateOnly birthDate, DateOnly today, int days)
    {
        for (var year = today.Year - 1; year <= today.Year + 1; year++)
        {
            var day = birthDate.Month == 2 && birthDate.Day == 29 && !DateTime.IsLeapYear(year) ? 28 : birthDate.Day;
            var birthday = new DateOnly(year, birthDate.Month, day);
            if (Math.Abs(today.DayNumber - birthday.DayNumber) <= days) return birthday.AddDays(-days);
        }
        return null;
    }

    public bool HasDiscount => DiscountType != CampaignDiscountType.None;
    public bool RequiresCoupon => CouponCode is not null;

    public bool MatchesCoupon(string? code) =>
        CouponCode is not null && string.Equals(CouponCode, code?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sepete uygulanacak indirim (₺). Kampanya o gün aktif değilse, indirimsizse, en düşük tutar tutmuyorsa
    /// veya indirim konusu ürün sepette yoksa 0. Kupon kontrolü burada yapılmaz (bkz. <see cref="CampaignDiscountPolicy"/>).
    /// </summary>
    public decimal DiscountFor(CampaignCart cart, DateOnly today)
    {
        if (PhaseOn(today) != CampaignPhase.Active || cart.Total <= 0) return 0;
        if (MinOrderAmount is { } min && cart.Total < min) return 0;

        var discount = DiscountType switch
        {
            CampaignDiscountType.Percentage => cart.Total * DiscountValue / 100m,
            CampaignDiscountType.FixedAmount => DiscountValue,
            CampaignDiscountType.FreeScent => cart.ScentAmount,
            CampaignDiscountType.FreePostcard => cart.PostcardAmount,
            _ => 0
        };
        return decimal.Round(Math.Min(discount, cart.Total), 2, MidpointRounding.AwayFromZero);
    }

    public void Publish() { IsPublished = true; Touch(); }

    public void Unpublish() { IsPublished = false; Touch(); }

    /// <returns>Varsa eski kapak dosyasının anahtarı (silinmesi için).</returns>
    public string? SetCover(string? imageKey)
    {
        var old = CoverImageKey;
        CoverImageKey = Guard.Optional(imageKey, "Kapak görseli", 300);
        Touch();
        return old == CoverImageKey ? null : old;
    }

    public CampaignPhase PhaseOn(DateOnly today) =>
        !IsPublished ? CampaignPhase.Draft
        : today < StartDate ? CampaignPhase.Upcoming
        : EndDate is { } end && today > end ? CampaignPhase.Ended
        : CampaignPhase.Active;

    /// <summary>Bitişe kalan gün (bugün son günse 0). Süresiz veya bitmişse null.</summary>
    public int? DaysLeftOn(DateOnly today) =>
        PhaseOn(today) == CampaignPhase.Active && EndDate is { } end ? end.DayNumber - today.DayNumber : null;

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>Uygulanan kampanya indirimi.</summary>
public sealed record AppliedCampaign(Guid CampaignId, string Title, decimal Discount);

/// <summary>
/// Domain servisi: bir siparişe uygulanacak kampanyayı seçer. Kampanyalar birleşmez; müşteriye en yüksek
/// indirimi sağlayan tek kampanya uygulanır. Kuponlu kampanya yalnızca kodu girildiğinde adaydır.
/// </summary>
public static class CampaignDiscountPolicy
{
    public static AppliedCampaign? Best(IEnumerable<Campaign> campaigns, CampaignCart cart, string? couponCode, DateOnly today,
        CampaignCustomer? customer = null) =>
        campaigns
            .Where(c => c.HasDiscount && (!c.RequiresCoupon || c.MatchesCoupon(couponCode)) && c.IsEligible(customer, today))
            .Select(c => (Campaign: c, Discount: c.DiscountFor(cart, today)))
            .Where(x => x.Discount > 0)
            .OrderByDescending(x => x.Discount)
            .ThenBy(x => x.Campaign.EndDate ?? DateOnly.MaxValue)
            .Select(x => new AppliedCampaign(x.Campaign.Id, x.Campaign.Title, x.Discount))
            .FirstOrDefault();
}

public interface ICampaignRepository
{
    Task<Campaign?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    /// <summary>Yayındaki, bitişi verilen günden önce olmayan kampanyalar (devam eden + yakında).</summary>
    Task<IReadOnlyList<Campaign>> ListCurrentAsync(DateOnly today, CancellationToken ct = default);
    Task<IReadOnlyList<Campaign>> ListEndedAsync(DateOnly today, int maxCount, CancellationToken ct = default);
    Task<IReadOnlyList<Campaign>> ListAllAsync(CancellationToken ct = default);
    /// <summary>Müşterinin kampanya kullanımları: iptal edilmemiş ödenmiş siparişler ve sonucu beklenen ödemeler.</summary>
    Task<IReadOnlyList<CampaignUse>> ListUsesAsync(Guid customerId, CancellationToken ct = default);
    void Add(Campaign campaign);
    void Remove(Campaign campaign);
}
