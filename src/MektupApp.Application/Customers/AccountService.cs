using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;

namespace MektupApp.Application.Customers;

/// <param name="ReferralCode">Sadece "Arkadaş" seçildiğinde dikkate alınır; boş bırakılabilir.</param>
public sealed record RegisterCommand(
    string FullName,
    string Email,
    string Phone,
    string Password,
    AcquisitionChannel? Channel = null,
    string? ReferralCode = null);

public sealed record AuthenticatedCustomer(Guid Id, string FullName, string Email, bool IsAdmin);

public sealed record ReferralInfoDto(string ReferralCode, int ReferredCount);

public sealed class AccountService(ICustomerRepository customers, IPasswordHasher hasher, IUnitOfWork unitOfWork)
{
    public const int MinPasswordLength = 8;

    public async Task<AuthenticatedCustomer> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(cmd.Password) || cmd.Password.Length < MinPasswordLength)
            throw new AppException($"Şifre en az {MinPasswordLength} karakter olmalıdır.");
        if (cmd.Channel is null)
            throw new AppException("Lütfen bizi nereden duyduğunuzu seçin.");

        var email = new Email(cmd.Email);
        if (await customers.EmailExistsAsync(email, ct))
            throw new AppException("Bu e-posta adresi ile kayıtlı bir üyelik zaten var.");

        // Referans no sadece "Arkadaş" seçiliyken okunur; diğer seçeneklerde gizli alanda kalan değer yok sayılır.
        Customer? referrer = null;
        var code = ReferralCode.Normalize(cmd.ReferralCode);
        if (cmd.Channel == AcquisitionChannel.Friend && code.Length > 0)
        {
            referrer = await customers.GetByReferralCodeAsync(code, ct);
            if (referrer is null || !referrer.IsActive)
                throw new AppException("Girdiğiniz referans no bulunamadı. Lütfen kontrol edin veya boş bırakın.");
        }

        var customer = Customer.Register(cmd.FullName, email, new PhoneNumber(cmd.Phone), hasher.Hash(cmd.Password),
            channel: cmd.Channel, referrer: referrer);

        // Referans no rastgele üretilir; çakışma ihtimali çok düşük olsa da kontrol edilir.
        while (await customers.ReferralCodeExistsAsync(customer.ReferralCode, ct))
            customer.RegenerateReferralCode();

        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(ct);
        return ToAuth(customer);
    }

    public async Task<ReferralInfoDto?> GetReferralInfoAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        if (c is null) return null;
        return new ReferralInfoDto(c.ReferralCode, await customers.CountReferredAsync(customerId, ct));
    }

    public async Task<AuthenticatedCustomer?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        Email parsed;
        try { parsed = new Email(email); }
        catch (Domain.Common.DomainException) { return null; }

        var customer = await customers.GetByEmailAsync(parsed, ct);
        if (customer is null || !hasher.Verify(customer.PasswordHash, password)) return null;
        if (!customer.IsActive)
            throw new AppException("Hesabınız askıya alınmıştır. Destek için bizimle iletişime geçin.");
        return ToAuth(customer);
    }

    /// <summary>Açık oturumların hâlâ geçerli olup olmadığını (engellenme / yetki değişimi) kontrol etmek için.</summary>
    public async Task<(bool IsActive, bool IsAdmin)?> GetAccessAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : (c.IsActive, c.IsAdmin);
    }

    public async Task<(string FullName, string Phone)?> GetProfileAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : (c.FullName, c.Phone.ToString());
    }

    private static AuthenticatedCustomer ToAuth(Customer c) => new(c.Id, c.FullName, c.Email.Value, c.IsAdmin);
}
