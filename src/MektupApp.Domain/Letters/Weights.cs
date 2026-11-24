using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

/// <summary>
/// Zarfa giren kalemlerin birim ağırlıkları (gram). Yönetim panelinden girilir;
/// zarfa basılan ve PTT'ye bildirilen tahmini ağırlık bunlardan hesaplanır.
/// </summary>
public sealed record PaperWeights
{
    public const decimal Max = 1000m;

    /// <summary>A4 mektup kağıdı, yaprak başı (80 g/m² A4 ≈ 5 g).</summary>
    public decimal LetterSheet { get; init; } = 5m;
    /// <summary>Fotoğraf kağıdı, fotoğraf başı.</summary>
    public decimal Photo { get; init; } = 12m;
    /// <summary>Kartpostal, adet başı.</summary>
    public decimal Postcard { get; init; } = 5m;
    /// <summary>Normal belge kağıdı, yaprak başı (PDF'in her sayfası bir yaprak).</summary>
    public decimal DocumentSheet { get; init; } = 5m;
    /// <summary>Boş zarf.</summary>
    public decimal Envelope { get; init; } = 6m;

    public void Validate()
    {
        if (new[] { LetterSheet, Photo, Postcard, DocumentSheet, Envelope }.Any(w => w is < 0 or > Max))
            throw new DomainException($"Ağırlıklar 0 ile {Max:0} gram arasında olmalıdır.");
    }
}

/// <summary>Ağırlık ayarlarını tutan aggregate (sistemde tek kayıt).</summary>
public sealed class WeightSettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000002");

    private WeightSettings() { } // EF Core

    public PaperWeights Weights { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static WeightSettings Create(PaperWeights weights)
    {
        weights.Validate();
        return new WeightSettings { Id = SingletonId, Weights = weights, UpdatedAt = DateTime.UtcNow, UpdatedBy = "sistem" };
    }

    public void Update(PaperWeights weights, string updatedBy)
    {
        weights.Validate();
        Weights = weights;
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }
}

public interface IWeightSettingsRepository
{
    Task<WeightSettings?> GetAsync(CancellationToken ct = default);
    void Add(WeightSettings settings);
}

public sealed record WeightLine(string Description, int Count, decimal Grams);

public sealed record WeightEstimate(IReadOnlyList<WeightLine> Lines, decimal TotalGrams)
{
    /// <summary>Zarfa basılan / PTT'ye bildirilen değer: yukarı yuvarlanmış tam gram.</summary>
    public int RoundedGrams => (int)Math.Ceiling(TotalGrams);
}

/// <summary>Zarfın tahmini ağırlığı: zarf + mektup yaprakları + fotoğraflar + kartpostal + belge yaprakları.</summary>
public static class LetterWeightPolicy
{
    public static WeightEstimate Estimate(int letterSheets, int photos, int documentSheets, bool hasPostcard, PaperWeights w)
    {
        var lines = new List<WeightLine> { new("Zarf", 1, w.Envelope) };
        if (letterSheets > 0) lines.Add(new("Mektup (A4)", letterSheets, w.LetterSheet * letterSheets));
        if (photos > 0) lines.Add(new("Fotoğraf", photos, w.Photo * photos));
        if (hasPostcard) lines.Add(new("Kartpostal", 1, w.Postcard));
        if (documentSheets > 0) lines.Add(new("Belge (yaprak)", documentSheets, w.DocumentSheet * documentSheets));
        return new WeightEstimate(lines, lines.Sum(l => l.Grams));
    }

    public static WeightEstimate Estimate(Letter letter, PaperWeights w) =>
        Estimate(letter.PageCount, letter.Photos.Count, letter.Documents.Sum(d => d.PageCount), letter.Postcard is not null, w);
}
