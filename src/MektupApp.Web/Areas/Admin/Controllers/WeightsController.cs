using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Letters;
using MektupApp.Domain.Common;
using MektupApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

/// <summary>
/// Birim ağırlıklar (A4 mektup kağıdı, fotoğraf, kartpostal, belge, zarf). Zarfa basılan ve
/// PTT'ye bildirilen tahmini ağırlık bu değerlerden hesaplanır.
/// </summary>
public class WeightsController(WeightService weights) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var info = await weights.GetInfoAsync(ct);
        return View(WeightFormViewModel.From(info.Weights, info.UpdatedAt, info.UpdatedBy));
    }

    [HttpPost]
    public async Task<IActionResult> Index(WeightFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var by = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "yönetici";
            await weights.UpdateAsync(model.ToWeights(), by, ct);
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Ağırlıklar güncellendi. Zarf çıktısındaki tahmini ağırlık bu değerlerle hesaplanır.";
        return RedirectToAction(nameof(Index));
    }
}
