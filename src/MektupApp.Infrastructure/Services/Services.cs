using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace MektupApp.Infrastructure.Services;

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
