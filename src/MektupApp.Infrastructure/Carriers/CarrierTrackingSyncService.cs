using MektupApp.Application.Letters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Carriers;

/// <summary>
/// Arka plan görevi: belirli aralıklarla kargodaki mektupların hareketlerini taşıyıcıdan çeker,
/// teslim edilenleri otomatik kapatır (puan kazanımı ve SMS de bu sırada tetiklenir).
/// </summary>
public sealed class CarrierTrackingSyncService(
    IServiceScopeFactory scopes,
    IOptions<PttOptions> options,
    ILogger<CarrierTrackingSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value.Sync;
        if (!o.Enabled)
        {
            logger.LogInformation("Taşıyıcı takip senkronizasyonu kapalı (Ptt:Sync:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, o.IntervalMinutes)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var shipping = scope.ServiceProvider.GetRequiredService<CarrierShippingService>();
                var s = await shipping.SyncShippedAsync(o.BatchSize, TimeSpan.FromMinutes(Math.Max(0, o.MinRecheckMinutes)), stoppingToken);
                if (s.Checked > 0)
                    logger.LogInformation("Takip senkronizasyonu: {Checked} sorgulandı, {Updated} yeni hareket, {Delivered} teslim, {Failed} hata",
                        s.Checked, s.Updated, s.Delivered, s.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Taşıyıcı takip senkronizasyonu başarısız.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
