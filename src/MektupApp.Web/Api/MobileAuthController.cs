using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Customers;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

/// <summary>
/// Giriş, kayıt ve oturum yenileme. Başarılı yanıt:
/// { tokenType: "Bearer", accessToken, expiresIn (sn), refreshToken }.
/// </summary>
[Route("api/mobile/auth")]
[AllowAnonymous]
public sealed class MobileAuthController(
    AccountService accounts,
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
            await accounts.GetAuthenticatedAsync(id, ct) is not { } customer)
            return Unauthorized(new ApiError("Oturumunuz sona erdi. Lütfen tekrar giriş yapın."));

        return Issue(customer);
    }

    private SignInResult Issue(AuthenticatedCustomer customer) =>
        SignIn(CustomerSignIn.CreatePrincipal(customer, MobileApi.Scheme), MobileApi.Scheme);
}
