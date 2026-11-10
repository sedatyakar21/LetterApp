using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MektupApp.Web.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetCustomerId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("Kullanıcı oturum açmamış."));
}

public static class EnumExtensions
{
    public static string DisplayName(this Enum value) =>
        value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name
        ?? value.ToString();
}

/// <summary>
/// İş kuralı hatalarını (DomainException / AppException) kullanıcıya mesaj olarak gösterir
/// ve isteğin geldiği sayfaya geri yönlendirir.
/// </summary>
public sealed class UserFacingExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not (DomainException or AppException)) return;
        // Mobil API JSON hata döner (ApiExceptionFilter); sayfaya yönlendirme yapılmaz.
        if (context.ActionDescriptor.EndpointMetadata.OfType<ApiControllerAttribute>().Any()) return;

        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        tempData["Error"] = context.Exception.Message;

        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        var isLocal = Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                      && uri.Host == context.HttpContext.Request.Host.Host;
        context.Result = new RedirectResult(isLocal ? uri!.PathAndQuery : "/");
        context.ExceptionHandled = true;
    }
}
