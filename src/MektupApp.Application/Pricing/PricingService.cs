using MektupApp.Application.Abstractions;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Pricing;

public sealed record PricingInfoDto(PriceList Prices, DateTime UpdatedAt, string? UpdatedBy);

/// <summary>Geçerli fiyat listesini okur ve yönetim panelinden güncellenmesini sağlar.</summary>
public sealed class PricingService(IPricingSettingsRepository settings, IUnitOfWork unitOfWork, PriceList defaults)
{
    public async Task<PriceList> GetCurrentAsync(CancellationToken ct = default) =>
        (await settings.GetAsync(ct))?.Prices ?? defaults;

    public async Task<PricingInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        return s is null
            ? new PricingInfoDto(defaults, DateTime.MinValue, null)
            : new PricingInfoDto(s.Prices, s.UpdatedAt, s.UpdatedBy);
    }

    public async Task UpdateAsync(PriceList prices, string updatedBy, CancellationToken ct = default)
    {
        var s = await settings.GetAsync(ct);
        if (s is null)
        {
            s = PricingSettings.Create(prices);
            settings.Add(s);
        }
        s.Update(prices, updatedBy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>İlk açılışta fiyat kaydı yoksa yapılandırmadaki varsayılanlarla oluşturur.</summary>
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (await settings.GetAsync(ct) is not null) return;
        settings.Add(PricingSettings.Create(defaults));
        await unitOfWork.SaveChangesAsync(ct);
    }
}
