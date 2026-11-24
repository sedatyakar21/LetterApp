using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Customers;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MektupApp.Web.Api;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(
    string? FullName,
    string? Email,
    string? Phone,
    string? Password,
    AcquisitionChannel? Channel,
    string? ReferralCode,
    DateOnly? BirthDate,
    bool AcceptTerms);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record PasswordResetOptionsResponse(bool EmailEnabled, bool SmsEnabled, int CodeLength);
public sealed record PasswordResetRequestBody(string? Email, PasswordResetMethod? Method);
public sealed record PasswordResetConfirmBody(string? Email, string? Code, string? NewPassword);

/// <summary>
/// Giriş, kayıt ve oturum yenileme. Başarılı yanıt:
/// { tokenType: "Bearer", accessToken, expiresIn (sn), refreshToken }.
/// </summary>
[Route("api/mobile/auth")]
[AllowAnonymous]
public sealed class MobileAuthController(
    AccountService accounts,
    PasswordResetService resets,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    TimeProvider time) : MobileApiController
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
            throw new AppException("E-posta ve şifre zorunludur.");
        var customer = await accounts.AuthenticateAsync(req.Email, req.Password, ct)
                       ?? throw new AppException("E-posta veya şifre hatalı.");
        return Issue(customer);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        if (!req.AcceptTerms) throw new AppException("Üyelik sözleşmesini onaylamalısınız.");
        if (string.IsNullOrWhiteSpace(req.FullName)) throw new AppException("Ad soyad zorunludur.");
        if (string.IsNullOrWhiteSpace(req.Phone)) throw new AppException("Cep telefonu zorunludur.");
        if (string.IsNullOrWhiteSpace(req.Email)) throw new AppException("E-posta zorunludur.");

        var customer = await accounts.RegisterAsync(new RegisterCommand(
            req.FullName.Trim(), req.Email.Trim(), req.Phone, req.Password ?? "", req.Channel,
            req.Channel == AcquisitionChannel.Friend ? req.ReferralCode : null, req.BirthDate), ct);
        return Issue(customer);
    }

    // ---------------- Şifremi unuttum ----------------

    /// <summary>Hangi sıfırlama yöntemleri açık: { emailEnabled, smsEnabled }.</summary>
    [HttpGet("password-reset/options")]
    public async Task<IActionResult> PasswordResetOptions(CancellationToken ct)
    {
        var o = await resets.GetOptionsAsync(ct);
        return Ok(new PasswordResetOptionsResponse(o.EmailEnabled, o.SmsEnabled, PasswordResetService.CodeLength));
    }

    /// <summary>
    /// Bağlantı (e-posta) veya kod (SMS) gönderir. Adres kayıtlı olmasa da aynı yanıt döner.
    /// E-posta bağlantısı sitede açılır (/sifre-sifirla); SMS kodu <c>password-reset/confirm</c> ile kullanılır.
    /// </summary>
    [HttpPost("password-reset/request"), EnableRateLimiting(PasswordResetRateLimit.Policy)]
    public async Task<IActionResult> PasswordResetRequest(PasswordResetRequestBody req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) throw new AppException("E-posta zorunludur.");
        var method = req.Method ?? PasswordResetMethod.Email;
        await resets.RequestAsync(req.Email.Trim(), method, ct);
        return Ok(new { message = method == PasswordResetMethod.Sms
            ? "Bu e-posta adresiyle kayıtlı bir hesap varsa, kayıtlı cep telefonuna doğrulama kodu gönderdik."
            : "Bu e-posta adresiyle kayıtlı bir hesap varsa, şifre sıfırlama bağlantısını gönderdik. Bağlantı 60 dakika geçerlidir." });
    }

    /// <summary>SMS kodu ile yeni şifre. Başarılıysa üyenin tüm açık oturumları kapanır; uygulama giriş ekranına döner.</summary>
    [HttpPost("password-reset/confirm"), EnableRateLimiting(PasswordResetRateLimit.Policy)]
    public async Task<IActionResult> PasswordResetConfirm(PasswordResetConfirmBody req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email)) throw new AppException("E-posta zorunludur.");
        await resets.ResetWithCodeAsync(req.Email.Trim(), req.Code, req.NewPassword ?? "", ct);
        return Ok(new { message = "Şifreniz değiştirildi. Yeni şifrenizle giriş yapabilirsiniz." });
    }

    /// <summary>
    /// Süresi dolan erişim token'ını yeniler. Üye engellendiyse yenilenmez; ad / yetki değiştiyse yeni token güncel bilgiyi taşır.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken ct)
    {
        var ticket = string.IsNullOrEmpty(req.RefreshToken)
            ? null
            : bearerOptions.Get(MobileApi.Scheme).RefreshTokenProtector.Unprotect(req.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expires || time.GetUtcNow() >= expires ||
            !Guid.TryParse(ticket.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ||
            await accounts.GetAccessAsync(id, ct) is not { } access ||
            // Şifre, bu oturum açıldıktan sonra değiştiyse (ör. "şifremi unuttum") yenileme yapılmaz.
            !access.AllowsSessionFrom(ticket.Principal.SignedInAt() ?? ticket.Properties.IssuedUtc) ||
            await accounts.GetAuthenticatedAsync(id, ct) is not { } customer)
            return Unauthorized(new ApiError("Oturumunuz sona erdi. Lütfen tekrar giriş yapın."));

        return Issue(customer);
    }

    private SignInResult Issue(AuthenticatedCustomer customer) =>
        SignIn(CustomerSignIn.CreatePrincipal(customer, MobileApi.Scheme), MobileApi.Scheme);
}
