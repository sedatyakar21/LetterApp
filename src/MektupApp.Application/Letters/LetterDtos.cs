using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

public sealed record AddressDto(string Line, string District, string City, string PostalCode)
{
    public PostalAddress ToDomain() => new(Line, District, City, PostalCode);
    public static AddressDto From(PostalAddress a) => new(a.Line, a.District, a.City, a.PostalCode);
}

public sealed record SaveLetterCommand(
    Guid? LetterId,
    Guid CustomerId,
    string SenderName,
    AddressDto SenderAddress,
    string RecipientName,
    RecipientType RecipientType,
    string? RecipientDetail,
    AddressDto RecipientAddress,
    string? Content,
    PaperColor Paper,
    EnvelopeColor Envelope,
    LetterFont Font,
    InkColor Ink,
    Scent Scent,
    PostcardCategory? PostcardCategory,
    int? PostcardDesign);

public sealed record LetterSummaryDto(
    Guid Id,
    string RecipientName,
    RecipientType RecipientType,
    LetterStatus Status,
    string Preview,
    DateTime UpdatedAt,
    string? TrackingCode);

public sealed record LetterPhotoDto(Guid Id, string OriginalFileName);

public sealed record StatusChangeDto(LetterStatus Status, string Note, DateTime At);

public sealed record LetterDetailsDto(
    Guid Id,
    Guid CustomerId,
    string SenderName,
    AddressDto SenderAddress,
    string RecipientName,
    RecipientType RecipientType,
    string? RecipientDetail,
    AddressDto RecipientAddress,
    string Content,
    LetterStyle Style,
    Scent Scent,
    Postcard? Postcard,
    LetterStatus Status,
    int PageCount,
    decimal? PaidAmount,
    PaymentMethod? PaymentMethod,
    string? TrackingCode,
    IReadOnlyList<LetterPhotoDto> Photos,
    IReadOnlyList<StatusChangeDto> History,
    bool IsEditable,
    bool CanCancel,
    bool CanDelete,
    bool CanChangeAddress,
    DateTime CreatedAt,
    decimal PointsUsed,
    decimal PointsToEarn,
    Guid? ReferrerCustomerId,
    decimal ReferralPointsToEarn,
    decimal PointsDiscount,
    string? Carrier,
    string? CarrierLastMovement,
    DateTime? CarrierLastMovementAt,
    DateTime? CarrierCheckedAt)
{
    public string? CampaignTitle { get; init; }
    public decimal CampaignDiscount { get; init; }

    public static LetterDetailsDto From(Letter l) => new(
        l.Id, l.CustomerId, l.SenderName, AddressDto.From(l.SenderAddress),
        l.Recipient.FullName, l.Recipient.Type, l.Recipient.Detail, AddressDto.From(l.Recipient.Address),
        l.Content, l.Style, l.Scent, l.Postcard, l.Status, l.PageCount, l.PaidAmount, l.PaymentMethod, l.TrackingCode,
        l.Photos.Select(p => new LetterPhotoDto(p.Id, p.OriginalFileName)).ToList(),
        l.History.Select(h => new StatusChangeDto(h.Status, h.Note, h.At)).ToList(),
        l.IsEditable, l.CanCancel, l.CanDelete, l.CanChangeAddress, l.CreatedAt,
        l.PointsUsed, l.PointsToEarn, l.ReferrerCustomerId, l.ReferralPointsToEarn, l.PointsDiscount,
        l.Carrier, l.CarrierLastMovement, l.CarrierLastMovementAt, l.CarrierCheckedAt)
    {
        CampaignTitle = l.CampaignTitle,
        CampaignDiscount = l.CampaignDiscount
    };
}

/// <param name="Total">Puan indirimi öncesi toplam.</param>
/// <param name="AmountToPay">Kart / mobil ödemeyle tahsil edilecek tutar (Total − PointsDiscount).</param>
/// <param name="PointsDiscount">Kullanılan puanın TL karşılığı.</param>
/// <param name="PointValue">1 puanın TL karşılığı.</param>
/// <param name="BalanceValue">Puan bakiyesinin TL karşılığı.</param>
/// <param name="PointsToEarn">Mektup postaya verildiğinde kazanılacak puan.</param>
public sealed record QuoteDto(
    Guid LetterId,
    PaymentMethod Method,
    IReadOnlyList<(string Description, decimal Amount)> Lines,
    decimal Total,
    bool UsePoints,
    decimal PointsBalance,
    decimal PointsUsed,
    decimal AmountToPay,
    decimal PointsToEarn,
    bool LoyaltyEnabled,
    decimal PointsDiscount,
    decimal PointValue,
    decimal BalanceValue,
    bool MobilePaymentAvailable = true,
    string ProviderLabel = "",
    bool ProviderIsSimulation = false)
{
    /// <summary>Uygulanan kampanya (yoksa null). İndirim puan kullanımından önce düşülür.</summary>
    public MektupApp.Domain.Campaigns.AppliedCampaign? Campaign { get; init; }
    /// <summary>Müşterinin girdiği kupon kodu (büyük harfe çevrilmiş).</summary>
    public string? CouponCode { get; init; }
    /// <summary>Girilen kupon, uygulanan kampanyanın kodu mu.</summary>
    public bool CouponApplied { get; init; }
    /// <summary>Girilen kod aktif, indirimli bir kampanyaya ait mi (uygulanmasa bile).</summary>
    public bool CouponRecognized { get; init; }
    /// <summary>Kod geçerli ama müşteri bu kampanyanın hedef kitlesinde değil ya da kullanım hakkı doldu.</summary>
    public bool CouponNotEligible { get; init; }
    public decimal CampaignDiscount => Campaign?.Discount ?? 0;
}

public sealed record TrackingResultDto(string TrackingCode, string RecipientInitials, string City, LetterStatus Status, IReadOnlyList<StatusChangeDto> History);
