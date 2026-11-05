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

        if (access is null || !access.Value.IsActive || access.Value.IsAdmin != hasAdminClaim)
            await RejectAsync(context);
    }

    private static Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
