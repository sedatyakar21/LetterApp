using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Customers;

public enum ContactChangeKind
{
    [Display(Name = "E-posta")] Email = 0,
    [Display(Name = "Cep telefonu")] Phone = 1
}

/// <summary>
/// E-posta / cep telefonu değişikliği talebi. Yeni adrese 6 haneli onay kodu gönderilir; kod doğru girilene kadar
/// üyenin kayıtlı bilgisi değişmez. Kodun yalnızca özeti saklanır; talep kısa süre geçerlidir, tek kullanımlıktır
/// ve yanlış deneme sayısı sınırlıdır.
/// </summary>
public sealed class ContactChangeRequest : AggregateRoot
{
    public const int MaxFailedAttempts = 5;
    /// <summary>Bir üyenin bu süre içinde isteyebileceği en çok kod sayısı (her tür için ayrı).</summary>
    public const int MaxRequestsPerWindow = 5;
    public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private ContactChangeRequest() { } // EF Core

    public Guid CustomerId { get; private set; }
    public ContactChangeKind Kind { get; private set; }
    /// <summary>Doğrulanınca uygulanacak değer (e-posta küçük harfli, telefon 5XXXXXXXXX).</summary>
    public string NewValue { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    /// <summary>Onaylandığı, iptal edildiği veya yeni talep / değişiklikle geçersizleştiği an.</summary>
    public DateTime? ClosedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public int FailedAttempts { get; private set; }

    public static ContactChangeRequest Create(Guid customerId, ContactChangeKind kind, string newValue, string codeHash, DateTime now)
    {
        if (!Enum.IsDefined(kind)) throw new DomainException("Geçersiz değişiklik türü.");
        return new ContactChangeRequest
        {
            CustomerId = customerId,
            Kind = kind,
            NewValue = Guard.Required(newValue, "Yeni değer", 200),
            CodeHash = Guard.Required(codeHash, "Kod", 64),
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };
    }

    public int RemainingAttempts => Math.Max(0, MaxFailedAttempts - FailedAttempts);

    public bool IsUsable(DateTime now) => ClosedAt is null && now < ExpiresAt && FailedAttempts < MaxFailedAttempts;

    public void RegisterFailedAttempt() => FailedAttempts++;

    public void Confirm(DateTime now)
    {
        if (!IsUsable(now)) throw new DomainException("Onay kodunun süresi dolmuş. Lütfen yeni kod isteyin.");
        ConfirmedAt = now;
        ClosedAt = now;
    }

    public void Close(DateTime now) => ClosedAt ??= now;
}

public interface IContactChangeRepository
{
    /// <summary>Üyenin açık (kapatılmamış, süresi dolmamış) talepleri.</summary>
    Task<IReadOnlyList<ContactChangeRequest>> ListOpenAsync(Guid customerId, DateTime now, CancellationToken ct = default);
    Task<int> CountSinceAsync(Guid customerId, ContactChangeKind kind, DateTime since, CancellationToken ct = default);
    void Add(ContactChangeRequest request);
}
