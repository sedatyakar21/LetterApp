using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Admin;
using MektupApp.Application.Letters;
using MektupApp.Domain.Letters;

namespace MektupApp.Web.Areas.Admin.Models;

/// <summary>Sayfalama bileşeni modeli. UrlFor sayfa numarasından bağlantı üretir.</summary>
public sealed record PagerModel(int Page, int TotalPages, int TotalCount, Func<int, string?> UrlFor);

public sealed class OrdersIndexViewModel
{
    public required PagedResult<AdminLetterRowDto> Result { get; init; }
    public required IReadOnlyDictionary<LetterStatus, int> Counts { get; init; }
    public LetterStatus? Status { get; init; }
    public string? Query { get; init; }
}

public sealed class OrderDetailsViewModel
{
    public required LetterDetailsDto Letter { get; init; }
    public CustomerRowDto? Customer { get; init; }
}

public sealed class CustomersIndexViewModel
{
    public required PagedResult<CustomerRowDto> Result { get; init; }
    public string? Query { get; init; }
}

public sealed class CustomerDetailsViewModel
{
    public required CustomerRowDto Customer { get; init; }
    public required PagedResult<AdminLetterRowDto> Letters { get; init; }
    public required MektupApp.Application.Loyalty.LoyaltyAccountDto Points { get; init; }
    public bool IsSelf { get; init; }
}

public sealed class PricingFormViewModel
{
    [Range(1, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Temel fiyat (1 sayfa + zarf + posta)")]
    public decimal BasePrice { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Ek sayfa (adet başı)")]
    public decimal ExtraPage { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Fotoğraf (adet başı)")]
    public decimal Photo { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Kartpostal")]
    public decimal Postcard { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Renkli kağıt")]
    public decimal ColoredPaper { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Renkli zarf")]
    public decimal ColoredEnvelope { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir tutar girin.")]
    [Display(Name = "Koku")]
    public decimal Scent { get; set; }

    [Range(0, 100, ErrorMessage = "0 ile 100 arasında bir oran girin.")]
    [Display(Name = "Mobil ödeme farkı (%)")]
    public decimal MobileSurchargePercent { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public PriceList ToPriceList() => new()
    {
        BasePrice = BasePrice,
        ExtraPage = ExtraPage,
        Photo = Photo,
        Postcard = Postcard,
        ColoredPaper = ColoredPaper,
        ColoredEnvelope = ColoredEnvelope,
        Scent = Scent,
        MobilePaymentSurchargeRate = MobileSurchargePercent / 100m
    };

    public static PricingFormViewModel From(PriceList p, DateTime updatedAt, string? updatedBy) => new()
    {
        BasePrice = p.BasePrice,
        ExtraPage = p.ExtraPage,
        Photo = p.Photo,
        Postcard = p.Postcard,
        ColoredPaper = p.ColoredPaper,
        ColoredEnvelope = p.ColoredEnvelope,
        Scent = p.Scent,
        MobileSurchargePercent = p.MobilePaymentSurchargeRate * 100m,
        UpdatedAt = updatedAt == DateTime.MinValue ? null : updatedAt,
        UpdatedBy = updatedBy
    };
}
