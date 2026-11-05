using MektupApp.Application.Abstractions;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

/// <summary>Operasyon ekibinin (basım / postalama) mektup üzerindeki işlemleri.</summary>
public sealed class FulfillmentService(ILetterRepository letters, IUnitOfWork unitOfWork)
{
    public async Task<LetterDetailsDto> GetAsync(Guid letterId, CancellationToken ct = default) =>
        LetterDetailsDto.From(await LoadAsync(letterId, ct));

    public Task StartProductionAsync(Guid letterId, CancellationToken ct = default) =>
        ExecuteAsync(letterId, l => l.StartProduction(), ct);

    /// <summary>Seçilen ödenmiş mektupları toplu olarak basıma alır. Başarılı olan sayısını döner.</summary>
    public async Task<int> StartProductionManyAsync(IEnumerable<Guid> letterIds, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var id in letterIds.Distinct())
        {
            var letter = await letters.GetAsync(id, ct);
            if (letter?.Status != LetterStatus.Paid) continue;
            letter.StartProduction();
            count++;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return count;
    }

    public Task ShipAsync(Guid letterId, string trackingCode, CancellationToken ct = default) =>
        ExecuteAsync(letterId, l => l.Ship(trackingCode), ct);

    public Task MarkDeliveredAsync(Guid letterId, CancellationToken ct = default) =>
        ExecuteAsync(letterId, l => l.MarkDelivered(), ct);

    public Task CancelAsync(Guid letterId, string reason, CancellationToken ct = default) =>
        ExecuteAsync(letterId, l => l.Cancel($"Yönetici tarafından iptal: {reason}"), ct);

    private async Task ExecuteAsync(Guid letterId, Action<Letter> action, CancellationToken ct)
    {
        var letter = await LoadAsync(letterId, ct);
        action(letter);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Letter> LoadAsync(Guid letterId, CancellationToken ct) =>
        await letters.GetAsync(letterId, ct) ?? throw new AppException("Mektup bulunamadı.");
}
