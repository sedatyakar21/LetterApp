using MektupApp.Application.Customers;
using MektupApp.Application.Loyalty;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

/// <summary>Müşterinin MektupPuan bakiyesi, hareketleri ve referans no'su.</summary>
[Authorize]
public class PointsController(LoyaltyService loyalty, AccountService accounts) : Controller
{
    [HttpGet("puanlarim")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var id = User.GetCustomerId();
        ViewBag.Settings = await loyalty.GetSettingsInfoAsync(ct);
        ViewBag.Referral = await accounts.GetReferralInfoAsync(id, ct);
        return View(await loyalty.GetAccountAsync(id, ct));
    }
}
