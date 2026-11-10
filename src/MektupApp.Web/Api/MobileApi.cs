using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Common;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MektupApp.Web.Api;

/// <summary>
/// Mobil uygulama (Flutter) API'si: /api/mobile/... JSON döner, kimlik doğrulama Bearer token iledir
/// (site çerezi kullanılmaz, bu yüzden CSRF koruması gerekmez).
/// </summary>
public static class MobileApi
{
    /// <summary>Token şeması: ASP.NET Core'un yerleşik BearerToken'ı (erişim + yenileme token'ı).</summary>
    public const string Scheme = "MobileBearer";
    public const string CorsPolicy = "MobileDev";
    public const string PaymentReturnPath = "/mobil/odeme-sonuc";
}

/// <summary>Mobil API denetleyicilerinin ortak ayarları.</summary>
[ApiController]
[IgnoreAntiforgeryToken]
[EnableCors(MobileApi.CorsPolicy)]
[ApiExceptionFilter]
[Authorize(AuthenticationSchemes = MobileApi.Scheme)]
[ActiveCustomerFilter]
public abstract class MobileApiController : ControllerBase
{
    protected Guid CustomerId => User.GetCustomerId();
}

/// <summary>İş kuralı hatalarını (DomainException / AppException) 400 + { message } olarak döner.</summary>
public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not (DomainException or AppException)) return;
        context.Result = new BadRequestObjectResult(new ApiError(context.Exception.Message));
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Token geçerli olsa bile hesap engellendiyse veya silindiyse isteği reddeder (401).
/// Site çerezindeki <see cref="SessionValidator"/>'ın mobil karşılığı.
/// </summary>
public sealed class ActiveCustomerFilterAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true) return;
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;

        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        var access = Guid.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? await accounts.GetAccessAsync(id, context.HttpContext.RequestAborted)
            : null;
        if (access is not { IsActive: true })
            context.Result = new UnauthorizedObjectResult(new ApiError("Oturumunuz sona erdi. Lütfen tekrar giriş yapın."));
    }
}

public sealed record ApiError(string Message);
