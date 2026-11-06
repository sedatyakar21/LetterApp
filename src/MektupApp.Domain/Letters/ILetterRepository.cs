namespace MektupApp.Domain.Letters;

/// <summary>Letter aggregate'i için repository sözleşmesi. Uygulaması Infrastructure katmanındadır.</summary>
public interface ILetterRepository
{
    Task<Letter?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Letter?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct = default);
    Task<IReadOnlyList<Letter>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Letter>> ListByStatusAsync(IEnumerable<LetterStatus> statuses, CancellationToken ct = default);
    /// <summary>Taşıyıcıdan güncellenmesi gereken kargodaki mektuplar (en eski kontrol edilen önce).</summary>
    Task<IReadOnlyList<Letter>> ListForCarrierSyncAsync(string carrier, DateTime checkedBefore, int maxCount, CancellationToken ct = default);
    void Add(Letter letter);
}
