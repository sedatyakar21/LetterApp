using System.Security.Cryptography;
using System.Text;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Notifications;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using Microsoft.Extensions.Logging;

namespace MektupApp.Application.Customers;

/// <summary>Onay bekleyen değişiklik (Hesabım'da kod giriş alanı için).</summary>
/// <param name="Destination">Kodun gönderildiği yeni adres, ekranda gösterilecek biçimde.</param>
public sealed record PendingContactChangeDto(ContactChangeKind Kind, string Destination, DateTime ExpiresAt, int RemainingAttempts);

/// <summary>
/// E-posta ve cep telefonu değişikliği (ayrı ayrı). Üye yeni değeri girer (şifreli hesaplarda mevcut şifresiyle);
/// yeni adrese 6 haneli onay kodu gönderilir (e-postaya mail, telefona SMS). Kod girilince değişiklik uygulanır.
/// Kodlar veritabanına yalnızca özet olarak yazılır ve bildirim geçmişine düşmez (doğrudan gönderilir).
/// </summary>
public sealed class ContactChangeService(
    IContactChangeRepository requests,
    ICustomerRepository customers,
    IPasswordHasher hasher,
    ISmsSender sms,
    IEmailSender email,
    NotificationTemplates templates,
    IUnitOfWork unitOfWork,
    ILogger<ContactChangeService> logger)
{
    public const int CodeLength = 6;
    private const string InvalidCode = "Onay kodu geçersiz veya süresi dolmuş. Lütfen yeni kod isteyin.";

    /// <summary>Yeni e-postaya onay kodu gönderir.</summary>
    public async Task<PendingContactChangeDto> RequestEmailChangeAsync(Guid customerId, string? newEmail, string? currentPassword,
        CancellationToken ct = default)
    {
        var customer = await LoadAsync(customerId, ct);
        Email parsed;
        try { parsed = new Email(newEmail ?? ""); }
        catch (DomainException) { throw new AppException("Geçerli bir e-posta adresi girin."); }
        if (parsed == customer.Email) throw new AppException("Yeni e-posta adresi mevcut adresinizle aynı.");
        VerifyPassword(customer, currentPassword);
        if (await customers.EmailExistsAsync(parsed, ct)) throw new AppException("Bu e-posta adresi başka bir üyelikte kayıtlı.");

        var (request, code) = await CreateAsync(customer, ContactChangeKind.Email, parsed.Value, ct);
        await SendAsync(request, () =>
        {
            var content = templates.EmailChangeCode(customer.FullName, code, ContactChangeRequest.Lifetime);
            return email.SendAsync(parsed.Value, content.EmailSubject, content.EmailText, content.EmailHtml, ct);
        }, "E-posta şu an gönderilemedi. Lütfen biraz sonra tekrar deneyin.", ct);
        return ToDto(request);
    }

    /// <summary>Yeni cep telefonuna SMS ile onay kodu gönderir.</summary>
    public async Task<PendingContactChangeDto> RequestPhoneChangeAsync(Guid customerId, string? newPhone, string? currentPassword,
        CancellationToken ct = default)
    {
        var customer = await LoadAsync(customerId, ct);
        PhoneNumber parsed;
        try { parsed = new PhoneNumber(newPhone ?? ""); }
        catch (DomainException) { throw new AppException("Geçerli bir cep telefonu numarası girin (5XX XXX XX XX)."); }
        if (parsed == customer.Phone) throw new AppException("Yeni numara mevcut numaranızla aynı.");
        VerifyPassword(customer, currentPassword);

        var (request, code) = await CreateAsync(customer, ContactChangeKind.Phone, parsed.Value, ct);
        await SendAsync(request, () => sms.SendAsync(parsed.Value, templates.PhoneChangeSms(code, ContactChangeRequest.Lifetime), ct),
            "SMS şu an gönderilemedi. Lütfen biraz sonra tekrar deneyin.", ct);
        return ToDto(request);
    }

    /// <summary>Onay kodunu doğrular ve değişikliği uygular.</summary>
    /// <returns>Güncel kimlik (web oturum çerezindeki e-posta yenilensin diye).</returns>
    public async Task<AuthenticatedCustomer> ConfirmAsync(Guid customerId, ContactChangeKind kind, string? code, CancellationToken ct = default)
    {
        var normalized = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (normalized.Length != CodeLength) throw new AppException($"Lütfen size gönderilen {CodeLength} haneli kodu girin.");

        var customer = await LoadAsync(customerId, ct);
        var now = DateTime.UtcNow;
        var request = (await requests.ListOpenAsync(customerId, now, ct))
            .Where(r => r.Kind == kind && r.IsUsable(now))
            .MaxBy(r => r.CreatedAt) ?? throw new AppException(InvalidCode);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(request.CodeHash), Encoding.ASCII.GetBytes(Hash(normalized))))
        {
            request.RegisterFailedAttempt();
            await unitOfWork.SaveChangesAsync(ct);
            throw new AppException(request.RemainingAttempts > 0
                ? $"Kod hatalı. {request.RemainingAttempts} deneme hakkınız kaldı."
                : "Kod hatalı ve deneme hakkınız doldu. Lütfen yeni kod isteyin.");
        }

        if (kind == ContactChangeKind.Email)
        {
            var newEmail = new Email(request.NewValue);
            // Kod beklerken adres başka bir üyelikte kullanılmaya başlamış olabilir.
            if (await customers.EmailExistsAsync(newEmail, ct))
            {
                request.Close(now);
                await unitOfWork.SaveChangesAsync(ct);
                throw new AppException("Bu e-posta adresi başka bir üyelikte kayıtlı.");
            }
            customer.ChangeEmail(newEmail);
        }
        else
        {
            customer.ChangePhone(new PhoneNumber(request.NewValue));
        }
        request.Confirm(now);
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Üye {CustomerId} {Kind} bilgisini onay koduyla değiştirdi.", customerId, kind);
        return new AuthenticatedCustomer(customer.Id, customer.FullName, customer.Email.Value, customer.IsAdmin);
    }

    /// <summary>Onay bekleyen talebi iptal eder (yanlış adres girildiyse).</summary>
    public async Task CancelAsync(Guid customerId, ContactChangeKind kind, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var r in (await requests.ListOpenAsync(customerId, now, ct)).Where(r => r.Kind == kind)) r.Close(now);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Onay bekleyen değişiklikler (en çok biri e-posta, biri telefon).</summary>
    public async Task<IReadOnlyList<PendingContactChangeDto>> ListPendingAsync(Guid customerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return (await requests.ListOpenAsync(customerId, now, ct))
            .Where(r => r.IsUsable(now))
            .GroupBy(r => r.Kind)
            .Select(g => ToDto(g.MaxBy(r => r.CreatedAt)!))
            .OrderBy(d => d.Kind)
            .ToList();
    }

    // ---------------- yardımcılar ----------------

    private async Task<Customer> LoadAsync(Guid customerId, CancellationToken ct) =>
        await customers.GetAsync(customerId, ct) is { IsActive: true } c ? c : throw new AppException("Üyelik bulunamadı.");

    /// <summary>Şifreli hesaplarda talep için mevcut şifre istenir (açık unutulmuş bir oturumdan hesabın ele geçirilmesine karşı).</summary>
    private void VerifyPassword(Customer customer, string? currentPassword)
    {
        if (customer.HasPassword && (string.IsNullOrEmpty(currentPassword) || !hasher.Verify(customer.PasswordHash, currentPassword)))
            throw new AppException("Mevcut şifreniz hatalı.");
    }

    private async Task<(ContactChangeRequest Request, string Code)> CreateAsync(Customer customer, ContactChangeKind kind, string newValue,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (await requests.CountSinceAsync(customer.Id, kind, now - ContactChangeRequest.RequestWindow, ct) >= ContactChangeRequest.MaxRequestsPerWindow)
            throw new AppException("Çok fazla onay kodu istendi. Lütfen bir saat sonra tekrar deneyin.");

        // Yeni talep aynı türdeki eskileri geçersiz kılar: yalnızca son gönderilen kod çalışır.
        foreach (var open in (await requests.ListOpenAsync(customer.Id, now, ct)).Where(r => r.Kind == kind)) open.Close(now);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var request = ContactChangeRequest.Create(customer.Id, kind, newValue, Hash(code), now);
        requests.Add(request);
        await unitOfWork.SaveChangesAsync(ct);
        return (request, code);
    }

    private async Task SendAsync(ContactChangeRequest request, Func<Task<SendResult>> send, string failMessage, CancellationToken ct)
    {
        SendResult result;
        try { result = await send(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Onay kodu gönderilemedi ({Kind}, üye {CustomerId})", request.Kind, request.CustomerId);
            result = SendResult.Fail(ex.Message);
        }
        if (result.Success) return;

        logger.LogWarning("Onay kodu gönderilemedi ({Kind}, üye {CustomerId}): {Error}", request.Kind, request.CustomerId, result.Error);
        request.Close(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        throw new AppException(failMessage);
    }

    private static PendingContactChangeDto ToDto(ContactChangeRequest r) => new(r.Kind,
        r.Kind == ContactChangeKind.Phone ? new PhoneNumber(r.NewValue).ToString() : r.NewValue, r.ExpiresAt, r.RemainingAttempts);

    private static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
