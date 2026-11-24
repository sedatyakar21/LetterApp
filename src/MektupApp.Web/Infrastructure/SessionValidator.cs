using System.Security.Claims;
using MektupApp.Application.Customers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Her istekte çerezdeki kullanıcının hâlâ aktif olduğunu ve yönetici yetkisinin
/// değişmediğini kontrol eder. Değiştiyse oturum kapatılır (yeniden giriş gerekir).
/// </summary>
public static class SessionValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var customerId))
        {
            await RejectAsync(context);
            return;
        }

        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        var access = await accounts.GetAccessAsync(customerId, context.HttpContext.RequestAborted);
        var hasAdminClaim = context.Principal!.IsInRole("Admin");

        // Şifre (ör. "şifremi unuttum" ile) oturum açıldıktan sonra değiştiyse bu oturum da kapanır.
        var signedInAt = context.Principal.SignedInAt() ?? context.Properties.IssuedUtc;
        if (access is null || !access.AllowsSessionFrom(signedInAt) || access.IsAdmin != hasAdminClaim)
            await RejectAsync(context);
    }

    private static Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
