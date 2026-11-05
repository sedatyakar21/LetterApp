using System.Net.Mail;
using System.Text.RegularExpressions;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Customers;

/// <summary>Müşteri aggregate root'u (üyelik).</summary>
public sealed class Customer : AggregateRoot
{
    private Customer() { } // EF Core

    public string FullName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public PhoneNumber Phone { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    public static Customer Register(string fullName, Email email, PhoneNumber phone, string passwordHash, bool isAdmin = false) =>
        new()
        {
            FullName = Guard.Required(fullName, "Ad soyad", 100),
            Email = email,
            Phone = phone,
            PasswordHash = Guard.Required(passwordHash, "Şifre", 500),
            IsAdmin = isAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    public void ChangePasswordHash(string passwordHash) =>
        PasswordHash = Guard.Required(passwordHash, "Şifre", 500);

    /// <summary>Hesabı askıya alır; kullanıcı giriş yapamaz ve açık oturumu sonlanır.</summary>
    public void Block()
    {
        if (IsAdmin) throw new DomainException("Yönetici hesabı engellenemez. Önce yönetici yetkisini kaldırın.");
        IsActive = false;
    }

    public void Unblock() => IsActive = true;

    public void GrantAdmin()
    {
        if (!IsActive) throw new DomainException("Engellenmiş bir hesaba yönetici yetkisi verilemez.");
        IsAdmin = true;
    }

    public void RevokeAdmin() => IsAdmin = false;
}

public sealed record Email
{
    public string Value { get; }

    public Email(string value)
    {
        var v = Guard.Required(value, "E-posta", 200).ToLowerInvariant();
        if (!MailAddress.TryCreate(v, out var parsed) || parsed.Address != v)
            throw new DomainException("Geçerli bir e-posta adresi girin.");
        Value = v;
    }

    public override string ToString() => Value;
}

/// <summary>Türkiye cep telefonu numarası; 5XXXXXXXXX biçiminde saklanır.</summary>
public sealed partial record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        var digits = NonDigits().Replace(value ?? string.Empty, "");
        if (digits.StartsWith("90")) digits = digits[2..];
        if (digits.StartsWith('0')) digits = digits[1..];
        if (digits.Length != 10 || digits[0] != '5')
            throw new DomainException("Geçerli bir cep telefonu numarası girin (5XX XXX XX XX).");
        Value = digits;
    }

    public override string ToString() => $"0{Value[..3]} {Value[3..6]} {Value[6..8]} {Value[8..]}";

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default);
    void Add(Customer customer);
}
