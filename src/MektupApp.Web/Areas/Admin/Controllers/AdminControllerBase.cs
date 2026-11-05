using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

/// <summary>Yönetim paneli controller'larının ortak tabanı: sadece "Admin" rolü erişebilir.</summary>
[Area("Admin")]
[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : Controller
{
    protected Guid CurrentUserId => User.GetCustomerId();

    /// <summary>İşlem sonrası mesaj gösterip isteğin geldiği sayfaya (yoksa verilen adrese) döner.</summary>
    protected IActionResult BackWith(string message, string fallbackUrl)
    {
        TempData["Success"] = message;
        var referer = Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == Request.Host.Host)
            return Redirect(uri.PathAndQuery);
        return Redirect(fallbackUrl);
    }
}
