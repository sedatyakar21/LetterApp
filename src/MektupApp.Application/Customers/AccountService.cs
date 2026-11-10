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
    string? ReferralCode = null,
    DateOnly? BirthDate = null);

public sealed record AuthenticatedCustomer(Guid Id, string FullName, string Email, bool IsAdmin);

public sealed record AccountDto(string FullName, string Email, string Phone, DateOnly MemberSince, DateOnly? BirthDate, string ReferralCode,
    bool HasPassword = true, bool GoogleLinked = false);

/// <summary>Google'ın doğruladığı kimlik bilgileri.</summary>
/// <param name="Subject">Google hesabının kalıcı kimliği ("sub").</param>
/// <param name="EmailVerified">Google e-postanın sahibini doğruladı mı; yalnızca doğrulanmışsa mevcut hesapla eşleştirilir.</param>
public sealed record GoogleIdentity(string Subject, string Email, string FullName, bool EmailVerified);

/// <summary>Google ile kayıt formunda üyeden ayrıca istenen bilgiler.</summary>
public sealed record GoogleRegisterCommand(
    GoogleIdentity Identity,
    string FullName,
    string Phone,
    AcquisitionChannel? Channel,
    string? ReferralCode = null,
    DateOnly? BirthDate = null);

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
        var referrer = await FindReferrerAsync(cmd.Channel, cmd.ReferralCode, ct);

        var customer = Customer.Register(cmd.FullName, email, new PhoneNumber(cmd.Phone), hasher.Hash(cmd.Password),
            channel: cmd.Channel, referrer: referrer);
        if (cmd.BirthDate is { } birth) customer.SetBirthDate(birth);
        customer.AnnounceRegistered();

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
        if (customer is { HasPassword: false })
            throw new AppException("Bu hesap Google ile oluşturuldu. Lütfen \"Google ile devam et\" butonunu kullanın.");
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

    /// <summary>Aktif üyenin güncel kimlik bilgileri (mobil oturum yenilenirken); engellenmiş veya silinmişse null.</summary>
    public async Task<AuthenticatedCustomer?> GetAuthenticatedAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is { IsActive: true } ? ToAuth(c) : null;
    }

    public async Task<(string FullName, string Phone)?> GetProfileAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : (c.FullName, c.Phone.ToString());
    }

    public async Task<AccountDto?> GetAccountAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : new AccountDto(c.FullName, c.Email.Value, c.Phone.ToString(), c.MemberSince, c.BirthDate, c.ReferralCode,
            c.HasPassword, c.GoogleSubject is not null);
    }

    // ---------------- Google ile giriş ----------------

    /// <summary>
    /// Google hesabı bağlı üyeyi bulur; yoksa Google'ın doğruladığı e-posta ile kayıtlı üyeye bağlar.
    /// Hiçbiri yoksa null döner: üye "Google ile kayıt" formunu doldurmalıdır.
    /// </summary>
    public async Task<AuthenticatedCustomer?> SignInWithGoogleAsync(GoogleIdentity id, CancellationToken ct = default)
    {
        var customer = await customers.GetByGoogleSubjectAsync(id.Subject, ct);
        if (customer is null)
        {
            Email email;
            try { email = new Email(id.Email); }
            catch (Domain.Common.DomainException) { throw new AppException("Google hesabınızın e-posta adresi okunamadı."); }

            var byEmail = await customers.GetByEmailAsync(email, ct);
            if (byEmail is null) return null;
            if (!id.EmailVerified)
                throw new AppException("Bu e-posta ile kayıtlı bir hesap var. Şifrenizle giriş yapıp Hesabım sayfasından Google'ı bağlayabilirsiniz.");
            byEmail.LinkGoogle(id.Subject);
            await unitOfWork.SaveChangesAsync(ct);
            customer = byEmail;
        }
        if (!customer.IsActive)
            throw new AppException("Hesabınız askıya alınmıştır. Destek için bizimle iletişime geçin.");
        return ToAuth(customer);
    }

    public async Task<AuthenticatedCustomer> RegisterWithGoogleAsync(GoogleRegisterCommand cmd, CancellationToken ct = default)
    {
        if (cmd.Channel is null)
            throw new AppException("Lütfen bizi nereden duyduğunuzu seçin.");
        if (await customers.GetByGoogleSubjectAsync(cmd.Identity.Subject, ct) is not null)
            throw new AppException("Bu Google hesabıyla zaten üyelik var. Lütfen giriş yapın.");

        var email = new Email(cmd.Identity.Email);
        if (await customers.EmailExistsAsync(email, ct))
            throw new AppException("Bu e-posta adresi ile kayıtlı bir üyelik zaten var. Şifrenizle giriş yapıp Hesabım sayfasından Google'ı bağlayabilirsiniz.");

        var referrer = await FindReferrerAsync(cmd.Channel, cmd.ReferralCode, ct);
        var customer = Customer.RegisterWithGoogle(cmd.FullName, email, new PhoneNumber(cmd.Phone), cmd.Identity.Subject,
            cmd.Channel, referrer);
        if (cmd.BirthDate is { } birth) customer.SetBirthDate(birth);
        customer.AnnounceRegistered();
        while (await customers.ReferralCodeExistsAsync(customer.ReferralCode, ct))
            customer.RegenerateReferralCode();

        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(ct);
        return ToAuth(customer);
    }

    /// <summary>Giriş yapmış üyenin hesabına Google bağlar (Hesabım sayfasından).</summary>
    public async Task LinkGoogleAsync(Guid customerId, GoogleIdentity id, CancellationToken ct = default)
    {
        var owner = await customers.GetByGoogleSubjectAsync(id.Subject, ct);
        if (owner is not null && owner.Id != customerId)
            throw new AppException("Bu Google hesabı başka bir üyeliğe bağlı.");
        var c = await customers.GetAsync(customerId, ct) ?? throw new AppException("Üyelik bulunamadı.");
        c.LinkGoogle(id.Subject);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnlinkGoogleAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct) ?? throw new AppException("Üyelik bulunamadı.");
        c.UnlinkGoogle();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Customer?> FindReferrerAsync(AcquisitionChannel? channel, string? referralCode, CancellationToken ct)
    {
        var code = ReferralCode.Normalize(referralCode);
        if (channel != AcquisitionChannel.Friend || code.Length == 0) return null;
        var referrer = await customers.GetByReferralCodeAsync(code, ct);
        if (referrer is null || !referrer.IsActive)
            throw new AppException("Girdiğiniz referans no bulunamadı. Lütfen kontrol edin veya boş bırakın.");
        return referrer;
    }

    /// <summary>Üye doğum tarihini bir kez girer; sonradan yalnızca yönetici değiştirebilir.</summary>
    public async Task SetBirthDateAsync(Guid customerId, DateOnly birthDate, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct) ?? throw new AppException("Üyelik bulunamadı.");
        c.SetBirthDate(birthDate);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static AuthenticatedCustomer ToAuth(Customer c) => new(c.Id, c.FullName, c.Email.Value, c.IsAdmin);
}
