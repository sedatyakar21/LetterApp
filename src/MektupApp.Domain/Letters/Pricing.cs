using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

/// <summary>Fiyat listesi (value object). Varsayılan değerler yapılandırmadan okunabilir.</summary>
public sealed record PriceList
{
    public decimal BasePrice { get; init; } = 290m;     // 1 sayfa + zarf + posta
    public decimal ExtraPage { get; init; } = 30m;
    public decimal Photo { get; init; } = 20m;
    public decimal Postcard { get; init; } = 35m;
    public decimal ColoredPaper { get; init; } = 15m;
    public decimal ColoredEnvelope { get; init; } = 15m;
    public decimal Scent { get; init; } = 40m;
    public decimal MobilePaymentSurchargeRate { get; init; } = 0.30m;

    public void Validate()
    {
        if (BasePrice <= 0) throw new DomainException("Temel fiyat sıfırdan büyük olmalıdır.");
        if (new[] { ExtraPage, Photo, Postcard, ColoredPaper, ColoredEnvelope, Scent }.Any(p => p < 0))
            throw new DomainException("Ek ücretler negatif olamaz.");
        if (MobilePaymentSurchargeRate is < 0 or > 1)
            throw new DomainException("Mobil ödeme farkı %0 ile %100 arasında olmalıdır.");
    }
}

/// <summary>
/// Geçerli fiyat listesini tutan aggregate (sistemde tek kayıt).
/// Fiyatlar yönetim panelinden değiştirilir; değişiklik yalnızca yeni ödemeleri etkiler.
/// </summary>
public sealed class PricingSettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    private PricingSettings() { } // EF Core

    public PriceList Prices { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static PricingSettings Create(PriceList prices)
    {
        prices.Validate();
        return new PricingSettings { Id = SingletonId, Prices = prices, UpdatedAt = DateTime.UtcNow, UpdatedBy = "sistem" };
    }

    public void Update(PriceList prices, string updatedBy)
    {
        prices.Validate();
        Prices = prices;
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }
}

public interface IPricingSettingsRepository
{
    Task<PricingSettings?> GetAsync(CancellationToken ct = default);
    void Add(PricingSettings settings);
}

public sealed record PriceLine(string Description, Money Amount);

public sealed record PriceBreakdown(IReadOnlyList<PriceLine> Lines, Money Total);

/// <summary>
/// Domain servisi: mektubun fiyatını seçilen özelliklere, ödeme yöntemine ve geçerli
/// fiyat listesine göre hesaplar. Durumsuzdur; tek bir entity'ye ait olmayan saf bir iş kuralıdır.
/// </summary>
public sealed class LetterPricingPolicy
{
    public PriceBreakdown Calculate(Letter letter, PaymentMethod method, PriceList prices)
    {
        var lines = new List<PriceLine> { new("Mektup (1 sayfa, zarf ve kayıtlı posta)", new Money(prices.BasePrice)) };

        var extraPages = letter.PageCount - 1;
        if (extraPages > 0)
            lines.Add(new($"Ek sayfa × {extraPages}", new Money(prices.ExtraPage) * extraPages));

        if (letter.Photos.Count > 0)
            lines.Add(new($"Fotoğraf × {letter.Photos.Count}", new Money(prices.Photo) * letter.Photos.Count));

        if (letter.Postcard is not null)
            lines.Add(new("Kartpostal", new Money(prices.Postcard)));

        if (letter.Style.HasColoredPaper)
            lines.Add(new("Renkli kağıt", new Money(prices.ColoredPaper)));

        if (letter.Style.HasColoredEnvelope)
            lines.Add(new("Renkli zarf", new Money(prices.ColoredEnvelope)));

        if (letter.Scent != Scent.None)
            lines.Add(new("Koku", new Money(prices.Scent)));

        var subtotal = lines.Aggregate(Money.Zero, (sum, l) => sum + l.Amount);

        if (method == PaymentMethod.Mobile)
            lines.Add(new($"Mobil ödeme operatör farkı (%{prices.MobilePaymentSurchargeRate * 100:0})",
                subtotal * prices.MobilePaymentSurchargeRate));

        var total = lines.Aggregate(Money.Zero, (sum, l) => sum + l.Amount);
        return new PriceBreakdown(lines, total);
    }
}
