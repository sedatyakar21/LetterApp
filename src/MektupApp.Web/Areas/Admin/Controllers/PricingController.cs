using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Common;
using MektupApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

/// <summary>Fiyat listesi yönetimi. Değişiklik sadece bundan sonraki ödemelere uygulanır.</summary>
public class PricingController(PricingService pricing) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var info = await pricing.GetInfoAsync(ct);
        return View(PricingFormViewModel.From(info.Prices, info.UpdatedAt, info.UpdatedBy));
    }

    [HttpPost]
    public async Task<IActionResult> Index(PricingFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var by = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "yönetici";
            await pricing.UpdateAsync(model.ToPriceList(), by, ct);
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Fiyatlar güncellendi. Yeni fiyatlar bundan sonraki ödemelerde geçerli.";
        return RedirectToAction(nameof(Index));
    }
}
