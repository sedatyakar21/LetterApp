using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Abstractions;

/// <summary>Bir use case içindeki tüm değişiklikleri tek işlemde kaydeder.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}

/// <param name="Permanent">Tekrar denemenin anlamı olmayan hata (geçersiz numara / adres, hatalı hesap bilgisi).</param>
public sealed record SendResult(bool Success, string? MessageId = null, string? Error = null, bool Permanent = false)
{
    public static SendResult Ok(string? messageId = null) => new(true, messageId);
    public static SendResult Fail(string error, bool permanent = false) => new(false, null, error, permanent);
}

public interface ISmsSender
{
    /// <summary>Gerçek sağlayıcı tanımlı değilse mesaj yalnızca log'a yazılır.</summary>
    bool IsSimulation { get; }
    string ProviderName { get; }
    /// <param name="phoneNumber">5XXXXXXXXX biçiminde cep telefonu.</param>
    Task<SendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

public interface IEmailSender
{
    bool IsSimulation { get; }
    string ProviderName { get; }
    Task<SendResult> SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

/// <summary>Uygulama katmanında oluşan, kullanıcıya gösterilebilir hata (ör. kayıt bulunamadı).</summary>
public sealed class AppException(string message) : Exception(message);
