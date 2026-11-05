using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MektupApp.Infrastructure.Services;

/// <summary>Geliştirme için sahte SMS servisi: mesajı log'a yazar. Gerçek sağlayıcı (Netgsm vb.) ile değiştirilir.</summary>
internal sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        logger.LogInformation("SMS -> {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Geliştirme için sahte ödeme sağlayıcısı: her ödemeyi onaylar. Kart bilgisi toplanmaz.
/// Canlı ortamda iyzico / PayTR gibi bir sağlayıcının 3D Secure akışı ile değiştirilmelidir.
/// </summary>
internal sealed class FakePaymentGateway(ILogger<FakePaymentGateway> logger) : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        var reference = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        logger.LogInformation("Simüle ödeme: {Letter} {Amount} {Method} -> {Ref}",
            request.LetterId, request.Amount, request.Method, reference);
        return Task.FromResult(new PaymentResult(true, reference, null));
    }
}

/// <summary>Dosyaları web kökü dışında, yerel diskte saklar (fotoğraflar herkese açık olmaz).</summary>
internal sealed class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var key = $"{Guid.NewGuid():N}{extension}";
        await using var file = File.Create(Path.Combine(rootPath, key));
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
    {
        var path = SafePath(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = SafePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string SafePath(string key) => Path.Combine(rootPath, Path.GetFileName(key));
}

/// <summary>ASP.NET Core Identity'nin PBKDF2 tabanlı şifre hash'leyicisini kullanır.</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Customer> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
