using System.Collections.Concurrent;
using MektupApp.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Carriers;

/// <summary>
/// Geliştirme için sahte taşıyıcı (Ptt:Enabled = false). Her gönderiyi kabul eder ve takipte
/// zamanla ilerleyen hareketler üretir: kabul → transfer → dağıtım → teslim.
/// Kayıtlar bellekte tutulur; uygulama yeniden başlarsa önceki barkodlar "bulunamadı" döner.
/// </summary>
public sealed class SimulatedPostalCarrier(IOptions<PttOptions> options, ILogger<SimulatedPostalCarrier> logger) : IPostalCarrier
{
    private readonly ConcurrentDictionary<string, (DateTime CreatedAt, string Recipient, string City, string SenderCity)> _shipments = new();
    private readonly TimeSpan _step = TimeSpan.FromSeconds(Math.Max(1, options.Value.Simulation.StepSeconds));

    public string Name => "PTT";
    public string EnvironmentLabel => "PTT · Simülasyon (gerçek gönderi yok)";
    public bool IsSimulation => true;

    public Task<CarrierShipmentResult> CreateShipmentAsync(CarrierShipmentRequest r, CancellationToken ct = default)
    {
        _shipments[r.Barcode] = (DateTime.UtcNow, r.Recipient.FullName, r.Recipient.City, r.Sender.City);
        logger.LogInformation("[Simülasyon] PTT gönderi kaydı: {Barcode} → {Recipient}, {City}", r.Barcode, r.Recipient.FullName, r.Recipient.City);
        return Task.FromResult(new CarrierShipmentResult(true, r.Barcode, $"SIM-{r.Reference}", null));
    }

    public Task<CarrierTrackingResult> TrackAsync(string barcode, CancellationToken ct = default)
    {
        if (!_shipments.TryGetValue(barcode, out var s))
            return Task.FromResult(CarrierTrackingResult.NotFound("Simülasyonda bu barkoda ait kayıt yok."));

        var steps = new (string Text, string Location)[]
        {
            ("KABUL EDİLDİ", $"{s.SenderCity} PİM"),
            ("TRANSFER MERKEZİNDE", "Ankara Transfer Merkezi"),
            ("DAĞITIMA ÇIKARILDI", $"{s.City} Dağıtım Merkezi"),
            ("TESLİM EDİLDİ", $"{s.City} Dağıtım Merkezi")
        };

        var reached = (int)Math.Min(steps.Length, 1 + (DateTime.UtcNow - s.CreatedAt).Ticks / _step.Ticks);
        var movements = steps.Take(reached)
            .Select((m, i) => new CarrierMovement(s.CreatedAt + _step * i, m.Text, m.Location))
            .ToList();
        var delivered = reached == steps.Length;

        return Task.FromResult(new CarrierTrackingResult(true, delivered, delivered ? s.Recipient : null, movements, null));
    }
}
