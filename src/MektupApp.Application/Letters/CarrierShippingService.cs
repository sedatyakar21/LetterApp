using MektupApp.Application.Abstractions;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

public sealed record CarrierSyncResult(bool NewMovement, bool Delivered, string? Error);

public sealed record CarrierSyncSummary(int Checked, int Updated, int Delivered, int Failed);

public sealed record BulkShipResult(int Shipped, IReadOnlyList<string> Errors);

/// <summary>
/// Taşıyıcı (PTT) entegrasyonu use case'leri: gönderi kaydı açıp barkod almak ve
/// kargodaki mektupların hareketlerini taşıyıcıdan çekmek.
/// </summary>
public sealed class CarrierShippingService(
    ILetterRepository letters,
    ICustomerRepository customers,
    IPostalCarrier carrier,
    IBarcodeAllocator barcodes,
    IUnitOfWork unitOfWork)
{
    public string CarrierName => carrier.Name;
    public string EnvironmentLabel => carrier.EnvironmentLabel;
    public bool IsSimulation => carrier.IsSimulation;

    /// <summary>Basımı biten mektup için taşıyıcıda gönderi kaydı açar, barkodu alır ve mektubu postaya verir.</summary>
    public async Task<string> ShipAsync(Guid letterId, CancellationToken ct = default)
    {
        var letter = await letters.GetAsync(letterId, ct) ?? throw new AppException("Mektup bulunamadı.");
        if (letter.Status != LetterStatus.InProduction)
            throw new DomainException("Sadece basımı tamamlanan mektuplar postaya verilebilir.");

        var sender = await customers.GetAsync(letter.CustomerId, ct);
        var barcode = await barcodes.NextAsync(ct);

        var result = await carrier.CreateShipmentAsync(BuildRequest(letter, sender, barcode), ct);
        if (!result.Succeeded)
            throw new AppException($"{carrier.Name} gönderi kaydı açılamadı: {result.Error ?? "bilinmeyen hata"}");

        letter.Ship(result.Barcode ?? barcode, carrier.Name, result.CarrierReference);
        await unitOfWork.SaveChangesAsync(ct);
        return letter.TrackingCode!;
    }

    /// <summary>Seçilen mektupları sırayla postaya verir; biri hata verirse diğerleri devam eder.</summary>
    public async Task<BulkShipResult> ShipManyAsync(IEnumerable<Guid> letterIds, CancellationToken ct = default)
    {
        var shipped = 0;
        var errors = new List<string>();
        foreach (var id in letterIds.Distinct())
        {
            try
            {
                await ShipAsync(id, ct);
                shipped++;
            }
            catch (Exception ex) when (ex is DomainException or AppException)
            {
                errors.Add(ex.Message);
            }
        }
        return new BulkShipResult(shipped, errors);
    }

    /// <summary>Tek bir mektubun hareketlerini taşıyıcıdan çeker; teslim edildiyse mektubu kapatır.</summary>
    public async Task<CarrierSyncResult> SyncAsync(Guid letterId, CancellationToken ct = default)
    {
        var letter = await letters.GetAsync(letterId, ct) ?? throw new AppException("Mektup bulunamadı.");
        if (letter.Status != LetterStatus.Shipped || letter.TrackingCode is null)
            throw new DomainException("Sadece kargodaki mektuplar sorgulanabilir.");

        var result = await SyncLetterAsync(letter, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Arka plan görevi için: en uzun süredir sorgulanmamış kargodaki mektupları taşıyıcıdan günceller.
    /// </summary>
    public async Task<CarrierSyncSummary> SyncShippedAsync(int maxCount, TimeSpan minInterval, CancellationToken ct = default)
    {
        var due = await letters.ListForCarrierSyncAsync(carrier.Name, DateTime.UtcNow - minInterval, maxCount, ct);
        int updated = 0, delivered = 0, failed = 0;

        foreach (var letter in due)
        {
            var r = await SyncLetterAsync(letter, ct);
            if (r.Error is not null) failed++;
            if (r.NewMovement) updated++;
            if (r.Delivered) delivered++;
            // Her mektup ayrı kaydedilir: biri hata verse de diğerlerinin ilerlemesi kaybolmaz.
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new CarrierSyncSummary(due.Count, updated, delivered, failed);
    }

    private async Task<CarrierSyncResult> SyncLetterAsync(Letter letter, CancellationToken ct)
    {
        CarrierTrackingResult tracking;
        try
        {
            tracking = await carrier.TrackAsync(letter.TrackingCode!, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            letter.MarkCarrierChecked();
            return new CarrierSyncResult(false, false, $"{carrier.Name} servisine ulaşılamadı: {ex.Message}");
        }

        if (!tracking.Found || tracking.Latest is null)
        {
            letter.MarkCarrierChecked();
            return new CarrierSyncResult(false, false, tracking.Error);
        }

        // Son kontrolden bu yana gelen tüm hareketleri sırayla kaydet (sadece sonuncuyu değil),
        // böylece takip sayfasında ara adımlar da görünür.
        var lastSeen = letter.CarrierLastMovementAt;
        var pending = tracking.Movements
            .Where(m => lastSeen is null || m.At is null || m.At > lastSeen)
            .DefaultIfEmpty(tracking.Latest);
        var isNew = false;
        foreach (var m in pending)
        {
            var text = m!.Location is null ? m.Description : $"{m.Description} ({m.Location})";
            isNew |= letter.RecordCarrierUpdate(text, m.At);
        }

        if (tracking.Delivered)
        {
            letter.MarkDelivered(tracking.DeliveredTo is null
                ? $"{carrier.Name}: Teslim edildi."
                : $"{carrier.Name}: Teslim edildi (teslim alan: {tracking.DeliveredTo}).");
            return new CarrierSyncResult(isNew, true, null);
        }
        return new CarrierSyncResult(isNew, false, null);
    }

    private static CarrierShipmentRequest BuildRequest(Letter letter, Customer? sender, string barcode)
    {
        var r = letter.Recipient;
        // Asker / cezaevi bilgisi adresin başına eklenir, zarfın üzerinde de böyle basılır.
        var recipientLine = r.Detail is null ? r.Address.Line : $"{r.Detail} - {r.Address.Line}";

        return new CarrierShipmentRequest(
            letter.Id,
            barcode,
            Reference: letter.Id.ToString("N")[..12].ToUpperInvariant(),
            Recipient: new CarrierParty(r.FullName, recipientLine, r.Address.District, r.Address.City, r.Address.PostalCode),
            Sender: new CarrierParty(letter.SenderName, letter.SenderAddress.Line, letter.SenderAddress.District,
                letter.SenderAddress.City, letter.SenderAddress.PostalCode, sender?.Phone.Value, sender?.Email.Value),
            WeightGrams: EstimateWeightGrams(letter));
    }

    /// <summary>Zarf + sayfa + fotoğraf + kartpostal için yaklaşık ağırlık (gram).</summary>
    internal static int EstimateWeightGrams(Letter letter) =>
        10 + letter.PageCount * 5 + letter.Photos.Count * 8 + (letter.Postcard is null ? 0 : 10);
}
