namespace MektupApp.Application.Abstractions;

// Posta / kargo taşıyıcısı portu. Uygulaması Infrastructure katmanında:
// PttPostalCarrier (gerçek PTT web servisleri) ve SimulatedPostalCarrier (geliştirme).

public sealed record CarrierParty(
    string FullName,
    string AddressLine,
    string District,
    string City,
    string PostalCode,
    string? Phone = null,
    string? Email = null);

/// <param name="Reference">Bizim taraftaki referans (taşıyıcı raporlarında görünür).</param>
public sealed record CarrierShipmentRequest(
    Guid LetterId,
    string Barcode,
    string Reference,
    CarrierParty Recipient,
    CarrierParty Sender,
    int WeightGrams);

/// <param name="CarrierReference">Taşıyıcının kayıt referansı (PTT'de "dosyaAdi"; silme işleminde gerekir).</param>
public sealed record CarrierShipmentResult(bool Succeeded, string? Barcode, string? CarrierReference, string? Error);

public sealed record CarrierMovement(DateTime? At, string Description, string? Location);

public sealed record CarrierTrackingResult(
    bool Found,
    bool Delivered,
    string? DeliveredTo,
    IReadOnlyList<CarrierMovement> Movements,
    string? Error)
{
    public CarrierMovement? Latest => Movements.Count == 0 ? null : Movements[^1];

    public static CarrierTrackingResult NotFound(string? error = null) => new(false, false, null, [], error);
}

public interface IPostalCarrier
{
    /// <summary>Kısa ad (ör. "PTT"); mektup geçmişinde görünür.</summary>
    string Name { get; }

    /// <summary>Ekranda gösterilecek ortam bilgisi (ör. "PTT · Test ortamı", "Simülasyon").</summary>
    string EnvironmentLabel { get; }

    bool IsSimulation { get; }

    Task<CarrierShipmentResult> CreateShipmentAsync(CarrierShipmentRequest request, CancellationToken ct = default);

    Task<CarrierTrackingResult> TrackAsync(string barcode, CancellationToken ct = default);
}

/// <summary>Taşıyıcının sözleşmeyle verdiği aralıktan sıradaki barkodu üretir.</summary>
public interface IBarcodeAllocator
{
    Task<string> NextAsync(CancellationToken ct = default);
}
