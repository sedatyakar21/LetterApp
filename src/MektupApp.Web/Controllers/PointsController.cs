using MektupApp.Application.Loyalty;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

/// <summary>Müşterinin MektupPuan bakiyesi ve hareketleri.</summary>
[Authorize]
public class PointsController(LoyaltyService loyalty) : Controller
{
    [HttpGet("puanlarim")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Settings = await loyalty.GetSettingsInfoAsync(ct);
        return View(await loyalty.GetAccountAsync(User.GetCustomerId(), ct));
    }
}
