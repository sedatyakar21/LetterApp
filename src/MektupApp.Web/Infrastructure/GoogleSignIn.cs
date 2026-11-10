using System.Security.Claims;
using MektupApp.Application.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Google ile giriş ayarı. <c>Authentication:Google:ClientId</c> ve <c>ClientSecret</c> verilmişse gerçek Google kullanılır.
/// Verilmemişse geliştirme ortamında simülasyon sayfası açılır, canlıda buton gizlenir.
/// </summary>
public sealed record GoogleSignIn(bool Enabled, bool Simulation)
{
    public const string Scheme = "Google";
    /// <summary>Google'dan dönen kimliği, üyelik tamamlanana kadar kısa süre tutan çerez.</summary>
    public const string ExternalScheme = "External";
    public const string ModeKey = "mode";
    public const string LinkMode = "link";

    public bool Available => Enabled || Simulation;

    public static GoogleIdentity? Read(ClaimsPrincipal? p)
    {
        var sub = p?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = p?.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email)) return null;
        var name = p!.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
        var verified = bool.TryParse(p.FindFirstValue("email_verified"), out var v) && v;
        return new GoogleIdentity(sub, email, name, verified);
    }
}

public static class CustomerSignIn
{
    /// <summary>Üyeyi site çerezine giriş yaptırır (e-posta/şifre ve Google girişinde ortak).</summary>
    public static Task SignInCustomerAsync(this HttpContext http, AuthenticatedCustomer customer) =>
        http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(customer, CookieAuthenticationDefaults.AuthenticationScheme),
            new AuthenticationProperties { IsPersistent = true });

    /// <summary>Üyenin kimlik bilgileri (site çerezi ve mobil uygulama token'ı aynı claim'leri taşır).</summary>
    public static ClaimsPrincipal CreatePrincipal(AuthenticatedCustomer customer, string authenticationType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.Email, customer.Email)
        };
        if (customer.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }
}
