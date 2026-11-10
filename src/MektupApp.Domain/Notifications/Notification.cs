using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Notifications;

public enum NotificationChannel
{
    [Display(Name = "SMS")] Sms = 0,
    [Display(Name = "E-posta")] Email = 1
}

/// <summary>Bildirim türleri (bayrak: ayarlarda kanal başına hangi türlerin açık olduğu tek sayıda tutulur).</summary>
[Flags]
public enum NotificationType
{
    None = 0,
    [Display(Name = "Hoş geldiniz (yeni üyelik)")] Welcome = 1,
    [Display(Name = "Sipariş alındı")] OrderPaid = 2,
    [Display(Name = "Mektup postaya verildi")] LetterShipped = 4,
    [Display(Name = "Mektup teslim edildi")] LetterDelivered = 8,
    [Display(Name = "Sipariş iptal / iade")] OrderCancelled = 16,
    [Display(Name = "MektupPuan kazanıldı")] PointsEarned = 32,
    [Display(Name = "Referans puanı kazanıldı")] ReferralPointsEarned = 64
}

public enum NotificationStatus
{
    [Display(Name = "Bekliyor")] Pending = 0,
    [Display(Name = "Gönderildi")] Sent = 1,
    [Display(Name = "Başarısız")] Failed = 2
}

/// <summary>
/// Gönderilecek bir SMS / e-posta (outbox kaydı, aggregate root). Olay anında oluşturulur, arka plan servisi gönderir.
/// Sağlayıcı geçici hata verirse artan aralıklarla yeniden denenir; deneme hakkı bitince "Başarısız" olur.
/// </summary>
public sealed class Notification : AggregateRoot
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];

    private Notification() { } // EF Core

    public Guid? CustomerId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationType Type { get; private set; }
    /// <summary>Telefon (5XXXXXXXXX) veya e-posta adresi.</summary>
    public string Recipient { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    /// <summary>SMS metni veya e-postanın düz metin hali.</summary>
    public string Body { get; private set; } = string.Empty;
    public string? HtmlBody { get; private set; }

    public NotificationStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static Notification Sms(Guid? customerId, NotificationType type, string phone, string text) =>
        Create(customerId, NotificationChannel.Sms, type, phone, null, text, null);

    public static Notification Email(Guid? customerId, NotificationType type, string email, string subject, string text, string html) =>
        Create(customerId, NotificationChannel.Email, type, email, Guard.Required(subject, "Konu", 200), text, html);

    private static Notification Create(Guid? customerId, NotificationChannel channel, NotificationType type,
        string recipient, string? subject, string body, string? html)
    {
        var now = DateTime.UtcNow;
        return new Notification
        {
            CustomerId = customerId,
            Channel = channel,
            Type = type,
            Recipient = Guard.Required(recipient, "Alıcı", 200),
            Subject = subject,
            Body = Guard.Required(body, "Mesaj", 4000),
            HtmlBody = html,
            Status = NotificationStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        };
    }

    public void MarkSent(string? providerMessageId)
    {
        Attempts++;
        Status = NotificationStatus.Sent;
        ProviderMessageId = Guard.Optional(providerMessageId, "Mesaj no", 100);
        LastError = null;
        SentAt = DateTime.UtcNow;
    }

    /// <param name="permanent">Tekrar denemenin anlamı yoksa (geçersiz numara, hatalı hesap) hemen başarısız say.</param>
    public void MarkFailed(string error, bool permanent = false)
    {
        Attempts++;
        LastError = Guard.Optional(error, "Hata", 500) ?? "Bilinmeyen hata";
        if (permanent || Attempts >= MaxAttempts)
        {
            Status = NotificationStatus.Failed;
            return;
        }
        NextAttemptAt = DateTime.UtcNow + Backoff[Math.Min(Attempts - 1, Backoff.Length - 1)];
    }

    /// <summary>Yönetici, başarısız bildirimi yeniden kuyruğa alır.</summary>
    public void Retry()
    {
        if (Status == NotificationStatus.Sent) throw new DomainException("Gönderilmiş bildirim tekrar gönderilemez.");
        Status = NotificationStatus.Pending;
        Attempts = 0;
        NextAttemptAt = DateTime.UtcNow;
    }
}

/// <summary>Hangi olayda hangi kanaldan bildirim gideceği (sistemde tek kayıt).</summary>
public sealed class NotificationSettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000003");

    public const NotificationType DefaultSms =
        NotificationType.OrderPaid | NotificationType.LetterShipped | NotificationType.LetterDelivered |
        NotificationType.OrderCancelled | NotificationType.PointsEarned | NotificationType.ReferralPointsEarned;

    public const NotificationType DefaultEmail =
        NotificationType.Welcome | NotificationType.OrderPaid | NotificationType.LetterShipped | NotificationType.LetterDelivered |
        NotificationType.OrderCancelled | NotificationType.PointsEarned | NotificationType.ReferralPointsEarned;

    private NotificationSettings() { } // EF Core

    public NotificationType SmsTypes { get; private set; }
    public NotificationType EmailTypes { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static NotificationSettings CreateDefault() =>
        new() { Id = SingletonId, SmsTypes = DefaultSms, EmailTypes = DefaultEmail, UpdatedAt = DateTime.UtcNow };

    public bool IsEnabled(NotificationChannel channel, NotificationType type) =>
        (channel == NotificationChannel.Sms ? SmsTypes : EmailTypes).HasFlag(type) && type != NotificationType.None;

    public void Update(NotificationType smsTypes, NotificationType emailTypes)
    {
        SmsTypes = smsTypes;
        EmailTypes = emailTypes;
        UpdatedAt = DateTime.UtcNow;
    }
}

public interface INotificationRepository
{
    Task<Notification?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListDueAsync(DateTime now, int maxCount, CancellationToken ct = default);
    Task<(IReadOnlyList<Notification> Items, int Total)> SearchAsync(NotificationChannel? channel, NotificationStatus? status,
        int page, int pageSize, CancellationToken ct = default);
    Task<NotificationSettings?> GetSettingsAsync(CancellationToken ct = default);
    void Add(Notification notification);
    void AddSettings(NotificationSettings settings);
}
