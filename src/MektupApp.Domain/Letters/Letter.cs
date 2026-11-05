using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

/// <summary>
/// Mektup aggregate root'u. Taslaktan teslimata kadar mektubun tüm yaşam döngüsünü
/// ve iş kurallarını (ne zaman düzenlenebilir, ne zaman ödenebilir, adres ne zaman
/// değiştirilebilir vb.) kendi içinde korur.
/// </summary>
public sealed class Letter : AggregateRoot
{
    public const int MaxContentLength = 25_000;
    public const int MaxPhotos = 10;

    private readonly List<LetterPhoto> _photos = [];
    private readonly List<LetterStatusChange> _history = [];

    private Letter() { } // EF Core

    public Guid CustomerId { get; private set; }
    public string SenderName { get; private set; } = string.Empty;
    public PostalAddress SenderAddress { get; private set; } = null!;
    public Recipient Recipient { get; private set; } = null!;
    public string Content { get; private set; } = string.Empty;
    public LetterStyle Style { get; private set; } = LetterStyle.Default;
    public Scent Scent { get; private set; }
    public Postcard? Postcard { get; private set; }

    public LetterStatus Status { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
    public decimal? PaidAmount { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public decimal PointsUsed { get; private set; }
    public decimal PointsToEarn { get; private set; }
    public string? PaymentReference { get; private set; }
    public string? TrackingCode { get; private set; }
    public bool IsDeleted { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<LetterPhoto> Photos => _photos.AsReadOnly();
    public IReadOnlyCollection<LetterStatusChange> History => _history.OrderBy(h => h.At).ToList().AsReadOnly();

    public int PageCount => LetterPages.Count(Content);
    public bool IsEditable => Status == LetterStatus.Draft;
    public bool CanChangeAddress => Status is LetterStatus.Draft or LetterStatus.Paid or LetterStatus.InProduction;
    public bool CanCancel => Status is LetterStatus.Draft or LetterStatus.Paid;
    public bool CanDelete => Status is LetterStatus.Draft or LetterStatus.Delivered or LetterStatus.Cancelled;

    // ---------- Oluşturma ----------

    public static Letter CreateDraft(Guid customerId, string senderName, PostalAddress senderAddress, Recipient recipient)
    {
        if (customerId == Guid.Empty) throw new DomainException("Müşteri bilgisi zorunludur.");

        var now = DateTime.UtcNow;
        var letter = new Letter
        {
            CustomerId = customerId,
            SenderName = Guard.Required(senderName, "Gönderen adı", 100),
            SenderAddress = senderAddress ?? throw new DomainException("Gönderen adresi zorunludur."),
            Recipient = recipient ?? throw new DomainException("Alıcı zorunludur."),
            Status = LetterStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        letter.AddHistory(LetterStatus.Draft, "Taslak oluşturuldu.");
        return letter;
    }

    // ---------- Taslak düzenleme ----------

    public void UpdateEnvelope(string senderName, PostalAddress senderAddress, Recipient recipient)
    {
        EnsureEditable();
        SenderName = Guard.Required(senderName, "Gönderen adı", 100);
        SenderAddress = senderAddress ?? throw new DomainException("Gönderen adresi zorunludur.");
        Recipient = recipient ?? throw new DomainException("Alıcı zorunludur.");
        Touch();
    }

    public void WriteContent(string? content)
    {
        EnsureEditable();
        content ??= string.Empty;
        if (content.Length > MaxContentLength)
            throw new DomainException($"Mektup en fazla {MaxContentLength} karakter olabilir.");
        Content = content;
        Touch();
    }

    public void ChangeStyle(LetterStyle style)
    {
        EnsureEditable();
        Style = style ?? throw new DomainException("Stil seçimi zorunludur.");
        Touch();
    }

    public void ChooseScent(Scent scent)
    {
        EnsureEditable();
        if (!Enum.IsDefined(scent)) throw new DomainException("Geçersiz koku seçimi.");
        Scent = scent;
        Touch();
    }

    public void AttachPostcard(Postcard? postcard)
    {
        EnsureEditable();
        Postcard = postcard;
        Touch();
    }

    public LetterPhoto AddPhoto(string storageKey, string originalFileName)
    {
        EnsureEditable();
        if (_photos.Count >= MaxPhotos)
            throw new DomainException($"Bir mektuba en fazla {MaxPhotos} fotoğraf eklenebilir.");

        var photo = new LetterPhoto(Id, storageKey, originalFileName);
        _photos.Add(photo);
        Touch();
        return photo;
    }

    public LetterPhoto RemovePhoto(Guid photoId)
    {
        EnsureEditable();
        var photo = _photos.FirstOrDefault(p => p.Id == photoId)
                    ?? throw new DomainException("Fotoğraf bulunamadı.");
        _photos.Remove(photo);
        Touch();
        return photo;
    }

    // ---------- Sipariş yaşam döngüsü ----------

    /// <summary>Ödeme alınmadan önce mektubun gönderilebilir durumda olduğunu doğrular.</summary>
    public void EnsureReadyForCheckout()
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(Content))
            throw new DomainException("Boş bir mektup gönderilemez.");
    }

    /// <param name="amount">Karttan / mobil ödemeyle tahsil edilen tutar (puan indirimi düşülmüş).</param>
    /// <param name="pointsUsed">Ödemede kullanılan MektupPuan (1 puan = 1 ₺).</param>
    /// <param name="pointsToEarn">Mektup postaya verildiğinde kazanılacak puan (ödeme anındaki ayara göre sabitlenir).</param>
    public void MarkPaid(Money amount, PaymentMethod method, string paymentReference,
        decimal pointsUsed = 0, decimal pointsToEarn = 0)
    {
        EnsureReadyForCheckout();
        if (pointsUsed < 0 || pointsToEarn < 0)
            throw new DomainException("Puan tutarı negatif olamaz.");

        PaidAmount = amount.Amount;
        PaidAt = DateTime.UtcNow;
        PaymentMethod = method;
        PaymentReference = Guard.Required(paymentReference, "Ödeme referansı", 100);
        PointsUsed = decimal.Round(pointsUsed, 2);
        PointsToEarn = decimal.Round(pointsToEarn, 2);

        var note = PointsUsed > 0
            ? $"Ödeme alındı ({amount} + {PointsUsed:N2} puan)."
            : $"Ödeme alındı ({amount}).";
        Transition(LetterStatus.Paid, note);
        Raise(new LetterPaid(Id, CustomerId, amount.Amount));
    }

    public void StartProduction()
    {
        if (Status != LetterStatus.Paid)
            throw new DomainException("Sadece ödemesi alınmış mektuplar basıma alınabilir.");
        Transition(LetterStatus.InProduction, "Mektup basılıyor ve zarflanıyor.");
    }

    public void Ship(string trackingCode)
    {
        if (Status != LetterStatus.InProduction)
            throw new DomainException("Sadece basımı tamamlanan mektuplar kargoya verilebilir.");
        TrackingCode = Letters.TrackingCode.Normalize(trackingCode);
        Transition(LetterStatus.Shipped, $"Postaya verildi. Takip kodu: {TrackingCode}");
        Raise(new LetterShipped(Id, CustomerId, TrackingCode, PointsToEarn));
    }

    public void MarkDelivered()
    {
        if (Status != LetterStatus.Shipped)
            throw new DomainException("Sadece kargodaki mektuplar teslim edildi olarak işaretlenebilir.");
        Transition(LetterStatus.Delivered, "Alıcıya teslim edildi.");
        Raise(new LetterDelivered(Id, CustomerId));
    }

    public void Cancel(string reason)
    {
        if (!CanCancel)
            throw new DomainException("Basıma alınmış veya yola çıkmış mektuplar iptal edilemez.");
        var wasPaid = Status == LetterStatus.Paid;
        Transition(LetterStatus.Cancelled, Guard.Required(reason, "İptal nedeni", 200));
        if (wasPaid) Raise(new LetterCancelledAfterPayment(Id, CustomerId, PaidAmount ?? 0, PointsUsed));
    }

    /// <summary>Mektup dağıtıma çıkmadan önce alıcı adresi değiştirilebilir.</summary>
    public void ChangeRecipientAddress(PostalAddress newAddress)
    {
        if (!CanChangeAddress)
            throw new DomainException("Dağıtıma çıkmış bir mektubun adresi değiştirilemez.");
        Recipient = new Recipient(Recipient.FullName, Recipient.Type, Recipient.Detail, newAddress);
        if (Status != LetterStatus.Draft)
            AddHistory(Status, "Alıcı adresi güncellendi.");
        Touch();
    }

    public void Delete()
    {
        if (!CanDelete)
            throw new DomainException("İşlemdeki bir mektup silinemez.");
        IsDeleted = true;
        Touch();
    }

    // ---------- Yardımcılar ----------

    private void EnsureEditable()
    {
        if (!IsEditable)
            throw new DomainException("Ödemesi yapılmış bir mektup düzenlenemez.");
    }

    private void Transition(LetterStatus next, string note)
    {
        Status = next;
        AddHistory(next, note);
        Touch();
    }

    private void AddHistory(LetterStatus status, string note) =>
        _history.Add(new LetterStatusChange(Id, status, note));

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>Mektuba eklenmiş fotoğraf. Sadece Letter aggregate'i üzerinden yönetilir.</summary>
public sealed class LetterPhoto : Entity
{
    private LetterPhoto() { } // EF Core

    internal LetterPhoto(Guid letterId, string storageKey, string originalFileName)
    {
        LetterId = letterId;
        StorageKey = Guard.Required(storageKey, "Dosya anahtarı", 300);
        OriginalFileName = Guard.Required(originalFileName, "Dosya adı", 255);
        AddedAt = DateTime.UtcNow;
    }

    public Guid LetterId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public DateTime AddedAt { get; private set; }
}

/// <summary>Takip ekranında gösterilen durum geçmişi kaydı.</summary>
public sealed class LetterStatusChange : Entity
{
    private LetterStatusChange() { } // EF Core

    internal LetterStatusChange(Guid letterId, LetterStatus status, string note)
    {
        LetterId = letterId;
        Status = status;
        Note = note;
        At = DateTime.UtcNow;
    }

    public Guid LetterId { get; private set; }
    public LetterStatus Status { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public DateTime At { get; private set; }
}

/// <summary>Sayfa sayısı hesaplama kuralı (fiyatlandırmada kullanılır).</summary>
public static class LetterPages
{
    public const int CharsPerPage = 2200;
    private const int CharsPerLineBreak = 60;

    public static int Count(string? content)
    {
        if (string.IsNullOrEmpty(content)) return 1;
        var lineBreaks = content.Count(c => c == '\n');
        var weighted = content.Length + lineBreaks * CharsPerLineBreak;
        return Math.Max(1, (int)Math.Ceiling(weighted / (double)CharsPerPage));
    }
}
