using MektupApp.Application.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Payments;

/// <summary>
/// Arka plan görevi: sonucu hiç gelmeyen (müşteri ödeme sayfasını kapattı vb.) ödeme denemelerini kapatır;
/// mektup tekrar düzenlenebilir olur ve ayrılan puan iade edilir.
/// </summary>
public sealed class PaymentExpiryService(IServiceScopeFactory scopes, IOptions<PaymentOptions> options, ILogger<PaymentExpiryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxAge = TimeSpan.FromMinutes(Math.Max(5, options.Value.ExpireAfterMinutes));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<PaymentService>().ExpireStaleAsync(maxAge, 100, stoppingToken);
                if (count > 0) logger.LogInformation("{Count} ödeme denemesinin süresi doldu.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Süresi dolan ödemeler kapatılamadı.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
