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

    /// <summary>Üyenin bizi nereden duyduğu (eski / sistem hesaplarında boş olabilir).</summary>
    public AcquisitionChannel? AcquisitionChannel { get; private set; }

    /// <summary>Bu üyenin arkadaşlarıyla paylaştığı benzersiz referans numarası.</summary>
    public string ReferralCode { get; private set; } = string.Empty;

    /// <summary>Bu üyeyi davet eden üye (referans no ile kayıt olduysa).</summary>
    public Guid? ReferredByCustomerId { get; private set; }

    /// <param name="referrer">"Arkadaş" seçilip geçerli bir referans no girildiyse, davet eden üye.</param>
    public static Customer Register(string fullName, Email email, PhoneNumber phone, string passwordHash,
        bool isAdmin = false, AcquisitionChannel? channel = null, Customer? referrer = null)
    {
        if (channel is { } c && !Enum.IsDefined(c))
            throw new DomainException("Geçersiz seçim: bizi nereden duydunuz?");
        if (referrer is not null)
        {
            if (channel != Customers.AcquisitionChannel.Friend)
                throw new DomainException("Referans no sadece 'Arkadaş' seçildiğinde kullanılabilir.");
            if (!referrer.IsActive)
                throw new DomainException("Bu referans no artık geçerli değil.");
        }

        var name = Guard.Required(fullName, "Ad soyad", 100);
        return new Customer
        {
            FullName = name,
            Email = email,
            Phone = phone,
            PasswordHash = Guard.Required(passwordHash, "Şifre", 500),
            IsAdmin = isAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            AcquisitionChannel = channel,
            ReferredByCustomerId = referrer?.Id,
            ReferralCode = Customers.ReferralCode.Generate(name)
        };
    }

    /// <summary>Üretilen kod başka bir üyede varsa (aynı baş harfler + aynı sayı) yenisi atanır.</summary>
    public void RegenerateReferralCode() => ReferralCode = Customers.ReferralCode.Generate(FullName);

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
    Task<Customer?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default);
    Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken ct = default);
    Task<int> CountReferredAsync(Guid referrerId, CancellationToken ct = default);
    void Add(Customer customer);
}
