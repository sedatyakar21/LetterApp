using MektupApp.Application.Abstractions;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

public sealed record WeightInfoDto(PaperWeights Weights, DateTime? UpdatedAt, string? UpdatedBy);

/// <summary>
/// Birim ağırlıkların (A4 mektup kağıdı, fotoğraf, kartpostal, belge, zarf) yönetimi ve
/// siparişin tahmini zarf ağırlığının hesaplanması. Kayıt yoksa varsayılan değerler kullanılır.
/// </summary>
public sealed class WeightService(IWeightSettingsRepository settings, IUnitOfWork unitOfWork)
{
    public async Task<PaperWeights> GetCurrentAsync(CancellationToken ct = default) =>
        (await settings.GetAsync(ct))?.Weights ?? new PaperWeights();

    public async Task<WeightInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        return s is null ? new WeightInfoDto(new PaperWeights(), null, null) : new WeightInfoDto(s.Weights, s.UpdatedAt, s.UpdatedBy);
    }

    public async Task UpdateAsync(PaperWeights weights, string updatedBy, CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        if (s is null)
        {
            s = WeightSettings.Create(weights);
            settings.Add(s);
        }
        s.Update(weights, updatedBy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Sipariş ekranı / zarf çıktısı için: mektubun tahmini ağırlığı.</summary>
    public static WeightEstimate Estimate(LetterDetailsDto l, PaperWeights w) =>
        LetterWeightPolicy.Estimate(l.PageCount, l.Photos.Count, l.Documents.Sum(d => d.PageCount), l.Postcard is not null, w);
}
