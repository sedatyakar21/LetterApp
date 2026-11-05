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

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
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

public sealed record PaymentRequest(Guid LetterId, Money Amount, PaymentMethod Method, string CustomerPhone);

public sealed record PaymentResult(bool Succeeded, string? Reference, string? Error);

/// <summary>Ödeme sağlayıcısı (iyzico, PayTR vb.) için port.</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);
}

/// <summary>Uygulama katmanında oluşan, kullanıcıya gösterilebilir hata (ör. kayıt bulunamadı).</summary>
public sealed class AppException(string message) : Exception(message);
