using System.Security.Cryptography;
using System.Text;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Notifications;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using Microsoft.Extensions.Logging;

namespace MektupApp.Application.Customers;

/// <summary>Şifremi unuttum ekranında hangi yöntemlerin sunulacağı.</summary>
public sealed record PasswordResetOptionsDto(bool EmailEnabled, bool SmsEnabled)
{
    public bool AnyEnabled => EmailEnabled || SmsEnabled;
}

/// <summary>
/// Şifremi unuttum. Üye e-posta adresini girer ve yöntemi seçer:
/// <list type="bullet">
/// <item>E-posta: tek kullanımlık, 60 dakika geçerli bir bağlantı gönderilir (/sifre-sifirla?token=...).</item>
/// <item>SMS: kayıtlı cep telefonuna 10 dakika geçerli, 6 haneli bir kod gönderilir; en çok 5 yanlış deneme.</item>
/// </list>
/// Yöntemler yönetim panelinden ayrı ayrı açılıp kapatılır. Gizli değerler veritabanına yalnızca özet olarak yazılır
/// ve bildirim geçmişine düşmez (doğrudan gönderilir). Şifre değişince üyenin tüm açık oturumları kapanır.
/// </summary>
public sealed class PasswordResetService(
    IPasswordResetRepository resets,
    ICustomerRepository customers,
    IPasswordHasher hasher,
    ISmsSender sms,
    IEmailSender email,
    NotificationTemplates templates,
    IUnitOfWork unitOfWork,
    ILogger<PasswordResetService> logger)
{
    public const int CodeLength = 6;
    private const string InvalidLink = "Bu şifre sıfırlama bağlantısı geçersiz veya süresi dolmuş. Lütfen yeniden talep edin.";
    private const string InvalidCode = "Kod geçersiz veya süresi dolmuş. Lütfen yeni kod isteyin.";

    // ---------------- ayarlar ----------------

    public async Task<PasswordResetOptionsDto> GetOptionsAsync(CancellationToken ct = default)
    {
        var s = await GetSettingsAsync(ct);
        return new PasswordResetOptionsDto(s.EmailEnabled, s.SmsEnabled);
    }

    public async Task UpdateSettingsAsync(bool emailEnabled, bool smsEnabled, string updatedBy, CancellationToken ct = default)
    {
        (await GetSettingsAsync(ct)).Update(emailEnabled, smsEnabled, updatedBy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PasswordResetSettings> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await resets.GetSettingsAsync(ct);
        if (settings is not null) return settings;
        settings = PasswordResetSettings.CreateDefault();
        resets.AddSettings(settings);
        return settings;
    }

    private async Task EnsureEnabledAsync(PasswordResetMethod method, CancellationToken ct)
    {
        if (!(await GetSettingsAsync(ct)).IsEnabled(method))
            throw new AppException(method == PasswordResetMethod.Sms
                ? "SMS ile şifre sıfırlama şu an kullanılamıyor."
                : "E-posta ile şifre sıfırlama şu an kullanılamıyor.");
    }

    // ---------------- talep ----------------

    /// <summary>
    /// Sıfırlama bağlantısı / kodu gönderir. Adres kayıtlı değilse veya hesap askıdaysa sessizce hiçbir şey yapmaz
    /// (ekranda her durumda aynı mesaj gösterilir).
    /// </summary>
    public async Task RequestAsync(string emailAddress, PasswordResetMethod method, CancellationToken ct = default)
    {
        await EnsureEnabledAsync(method, ct);
        var parsed = ParseEmail(emailAddress);

        var customer = await customers.GetByEmailAsync(parsed, ct);
        if (customer is not { IsActive: true }) return;

        var now = DateTime.UtcNow;
        if (await resets.CountSinceAsync(customer.Id, now - PasswordResetRequest.RequestWindow, ct) >= PasswordResetRequest.MaxRequestsPerWindow)
            throw new AppException("Çok fazla şifre sıfırlama talebi yapıldı. Lütfen bir saat sonra tekrar deneyin.");

        // Yeni talep öncekileri geçersiz kılar: yalnızca son gönderilen bağlantı / kod çalışır.
        foreach (var open in await resets.ListOpenAsync(customer.Id, now, ct)) open.Close(now);

        var secret = method == PasswordResetMethod.Email ? NewToken() : NewCode();
        var request = PasswordResetRequest.Create(customer.Id, method, Hash(secret), now);
        resets.Add(request);
        await unitOfWork.SaveChangesAsync(ct);

        var lifetime = PasswordResetRequest.LifetimeOf(method);
        SendResult result;
        try
        {
            if (method == PasswordResetMethod.Email)
            {
                var content = templates.PasswordResetEmail(customer.FullName, templates.PasswordResetUrl(secret), lifetime);
                result = await email.SendAsync(customer.Email.Value, content.EmailSubject, content.EmailText, content.EmailHtml, ct);
            }
            else
            {
                result = await sms.SendAsync(customer.Phone.Value, templates.PasswordResetSms(secret, lifetime), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Şifre sıfırlama gönderilemedi ({Method}, üye {CustomerId})", method, customer.Id);
            result = SendResult.Fail(ex.Message);
        }

        if (!result.Success)
        {
            logger.LogWarning("Şifre sıfırlama gönderilemedi ({Method}, üye {CustomerId}): {Error}", method, customer.Id, result.Error);
            request.Close(DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
            throw new AppException(method == PasswordResetMethod.Sms
                ? "SMS şu an gönderilemedi. Lütfen biraz sonra tekrar deneyin veya e-posta ile sıfırlayın."
                : "E-posta şu an gönderilemedi. Lütfen biraz sonra tekrar deneyin.");
        }
    }

    // ---------------- e-posta bağlantısı ----------------

    /// <summary>Bağlantı hâlâ geçerli mi (yeni şifre formunu göstermeden önce).</summary>
    public async Task<bool> IsTokenValidAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var request = await resets.GetBySecretHashAsync(Hash(token.Trim()), ct);
        return request is { Method: PasswordResetMethod.Email } && request.IsUsable(DateTime.UtcNow)
               && (await GetSettingsAsync(ct)).EmailEnabled;
    }

    public async Task ResetWithTokenAsync(string? token, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        await EnsureEnabledAsync(PasswordResetMethod.Email, ct);
        if (string.IsNullOrWhiteSpace(token)) throw new AppException(InvalidLink);

        var request = await resets.GetBySecretHashAsync(Hash(token.Trim()), ct);
        if (request is not { Method: PasswordResetMethod.Email } || !request.IsUsable(DateTime.UtcNow))
            throw new AppException(InvalidLink);

        await CompleteAsync(request, newPassword, InvalidLink, ct);
    }

    // ---------------- SMS kodu ----------------

    /// <returns>Kalan deneme hakkı bilgisiyle birlikte hata fırlatır; başarılıysa şifre değişmiştir.</returns>
    public async Task ResetWithCodeAsync(string emailAddress, string? code, string newPassword, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);
        await EnsureEnabledAsync(PasswordResetMethod.Sms, ct);
        var normalized = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (normalized.Length != CodeLength) throw new AppException($"Lütfen SMS ile gelen {CodeLength} haneli kodu girin.");

        var customer = await customers.GetByEmailAsync(ParseEmail(emailAddress), ct);
        if (customer is null) throw new AppException(InvalidCode);

        var now = DateTime.UtcNow;
        var request = (await resets.ListOpenAsync(customer.Id, now, ct))
            .Where(r => r.Method == PasswordResetMethod.Sms && r.IsUsable(now))
            .MaxBy(r => r.CreatedAt);
        if (request is null) throw new AppException(InvalidCode);

        if (!SecretEquals(request.SecretHash, Hash(normalized)))
        {
            request.RegisterFailedAttempt();
            await unitOfWork.SaveChangesAsync(ct);
            throw new AppException(request.RemainingAttempts > 0
                ? $"Kod hatalı. {request.RemainingAttempts} deneme hakkınız kaldı."
                : "Kod hatalı ve deneme hakkınız doldu. Lütfen yeni kod isteyin.");
        }

        await CompleteAsync(request, newPassword, InvalidCode, ct);
    }

    // ---------------- ortak ----------------

    private async Task CompleteAsync(PasswordResetRequest request, string newPassword, string invalidMessage, CancellationToken ct)
    {
        var customer = await customers.GetAsync(request.CustomerId, ct);
        if (customer is not { IsActive: true }) throw new AppException(invalidMessage);

        customer.ChangePasswordHash(hasher.Hash(newPassword));
        var now = DateTime.UtcNow;
        request.Close(now);
        foreach (var open in await resets.ListOpenAsync(customer.Id, now, ct)) open.Close(now);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Üye {CustomerId} şifresini sıfırladı ({Method}).", customer.Id, request.Method);
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < AccountService.MinPasswordLength)
            throw new AppException($"Şifre en az {AccountService.MinPasswordLength} karakter olmalıdır.");
    }

    private static Email ParseEmail(string? value)
    {
        try { return new Email(value ?? ""); }
        catch (DomainException) { throw new AppException("Geçerli bir e-posta adresi girin."); }
    }

    /// <summary>Bağlantıdaki anahtar: 32 rastgele bayt, URL'de güvenli Base64.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    internal static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static bool SecretEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
