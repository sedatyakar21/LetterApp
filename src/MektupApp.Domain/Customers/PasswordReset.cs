using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Customers;

/// <summary>Şifremi unuttum: sıfırlama bağlantısı e-postayla ya da doğrulama kodu SMS ile gönderilir.</summary>
public enum PasswordResetMethod
{
    [Display(Name = "E-posta")] Email = 0,
    [Display(Name = "SMS")] Sms = 1
}

/// <summary>Hangi şifre sıfırlama yönteminin açık olduğu (sistemde tek kayıt; yönetim panelinden değiştirilir).</summary>
public sealed class PasswordResetSettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000004");

    private PasswordResetSettings() { } // EF Core

    public bool EmailEnabled { get; private set; }
    public bool SmsEnabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Varsayılan: e-posta açık, SMS kapalı (her SMS ücretlidir).</summary>
    public static PasswordResetSettings CreateDefault() =>
        new() { Id = SingletonId, EmailEnabled = true, SmsEnabled = false, UpdatedAt = DateTime.UtcNow, UpdatedBy = "sistem" };

    public bool AnyEnabled => EmailEnabled || SmsEnabled;

    public bool IsEnabled(PasswordResetMethod method) => method switch
    {
        PasswordResetMethod.Email => EmailEnabled,
        PasswordResetMethod.Sms => SmsEnabled,
        _ => false
    };

    public void Update(bool emailEnabled, bool smsEnabled, string updatedBy)
    {
        EmailEnabled = emailEnabled;
        SmsEnabled = smsEnabled;
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Bir şifre sıfırlama talebi. Gönderilen gizli değerin (bağlantıdaki anahtar veya SMS kodu) yalnızca özeti saklanır;
/// talep kısa sürede geçersizleşir, tek kullanımlıktır ve SMS kodunda yanlış deneme sayısı sınırlıdır.
/// </summary>
public sealed class PasswordResetRequest : AggregateRoot
{
    public const int MaxFailedAttempts = 5;
    /// <summary>Bir üyenin bu süre içinde isteyebileceği en çok sıfırlama sayısı.</summary>
    public const int MaxRequestsPerWindow = 3;
    public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
    public static readonly TimeSpan EmailLifetime = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan SmsLifetime = TimeSpan.FromMinutes(10);

    private PasswordResetRequest() { } // EF Core

    public Guid CustomerId { get; private set; }
    public PasswordResetMethod Method { get; private set; }
    /// <summary>Gizli değerin SHA-256 özeti (hex). Gizli değerin kendisi hiçbir yerde saklanmaz.</summary>
    public string SecretHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    /// <summary>Kullanıldığı veya yeni talep / şifre değişikliğiyle geçersiz kılındığı an.</summary>
    public DateTime? ClosedAt { get; private set; }
    public int FailedAttempts { get; private set; }

    public static PasswordResetRequest Create(Guid customerId, PasswordResetMethod method, string secretHash, DateTime now)
    {
        if (!Enum.IsDefined(method)) throw new DomainException("Geçersiz şifre sıfırlama yöntemi.");
        return new PasswordResetRequest
        {
            CustomerId = customerId,
            Method = method,
            SecretHash = Guard.Required(secretHash, "Anahtar", 64),
            CreatedAt = now,
            ExpiresAt = now + (method == PasswordResetMethod.Email ? EmailLifetime : SmsLifetime)
        };
    }

    public static TimeSpan LifetimeOf(PasswordResetMethod method) =>
        method == PasswordResetMethod.Email ? EmailLifetime : SmsLifetime;

    public int RemainingAttempts => Math.Max(0, MaxFailedAttempts - FailedAttempts);

    public bool IsUsable(DateTime now) => ClosedAt is null && now < ExpiresAt && FailedAttempts < MaxFailedAttempts;

    /// <summary>Yanlış kod girildi; deneme hakkı bitince talep kullanılamaz hale gelir.</summary>
    public void RegisterFailedAttempt() => FailedAttempts++;

    /// <summary>Şifre değiştirildi ya da yeni talep oluşturuldu: bu talep artık kullanılamaz.</summary>
    public void Close(DateTime now) => ClosedAt ??= now;
}

public interface IPasswordResetRepository
{
    Task<PasswordResetSettings?> GetSettingsAsync(CancellationToken ct = default);
    void AddSettings(PasswordResetSettings settings);

    Task<PasswordResetRequest?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default);
    /// <summary>Üyenin hâlâ açık (kapatılmamış ve süresi dolmamış) talepleri.</summary>
    Task<IReadOnlyList<PasswordResetRequest>> ListOpenAsync(Guid customerId, DateTime now, CancellationToken ct = default);
    Task<int> CountSinceAsync(Guid customerId, DateTime since, CancellationToken ct = default);
    void Add(PasswordResetRequest request);
}
