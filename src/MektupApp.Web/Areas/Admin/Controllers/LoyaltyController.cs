using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Loyalty;
using MektupApp.Domain.Common;
using MektupApp.Domain.Loyalty;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class LoyaltySettingsForm
{
    [Display(Name = "Puan kazanımı açık")]
    public bool IsEnabled { get; set; }

    [Display(Name = "Kazanım türü")]
    public RewardType RewardType { get; set; }

    [Range(0, 10000, ErrorMessage = "Geçerli bir değer girin.")]
    [Display(Name = "Değer")]
    public decimal Value { get; set; }

    [Display(Name = "Referans kazancı açık")]
    public bool ReferralEnabled { get; set; }

    public RewardType ReferralRewardType { get; set; }

    [Range(0, 10000, ErrorMessage = "Geçerli bir değer girin.")]
    public decimal ReferralValue { get; set; }

    // Sınırlar tr-TR kültüründe "0.01" olarak okunamadığı için kültürden bağımsız parse edilir.
    [Range(typeof(decimal), "0.01", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "1 puanın değeri 0,01 ₺ ile 1.000 ₺ arasında olmalıdır.")]
    [Display(Name = "1 puanın TL karşılığı")]
    public decimal PointValue { get; set; } = 1m;

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public LoyaltySettingsInput ToInput() =>
        new(IsEnabled, RewardType, Value, ReferralEnabled, ReferralRewardType, ReferralValue, PointValue);
}

/// <summary>MektupPuan ayarları ve müşteri bakiyesi düzeltmeleri.</summary>
public class LoyaltyController(LoyaltyService loyalty) : AdminControllerBase
{
    private string AdminName => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "yönetici";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var s = await loyalty.GetSettingsInfoAsync(ct);
        return View(new LoyaltySettingsForm
        {
            IsEnabled = s.IsEnabled,
            RewardType = s.RewardType,
            Value = s.Value,
            ReferralEnabled = s.ReferralEnabled,
            ReferralRewardType = s.ReferralRewardType,
            ReferralValue = s.ReferralValue,
            PointValue = s.PointValue,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(LoyaltySettingsForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        try
        {
            await loyalty.UpdateSettingsAsync(form.ToInput(), AdminName, ct);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }

        TempData["Success"] = "Puan ayarları kaydedildi. Yeni ayar bundan sonraki ödemelerde geçerli.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Müşteri bakiyesine elle puan ekler (+) veya düşer (−).</summary>
    [HttpPost]
    public async Task<IActionResult> Adjust(Guid customerId, decimal points, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("Düzeltme için açıklama girin.");
        await loyalty.AdjustAsync(customerId, points, reason, AdminName, ct);
        return BackWith($"Bakiye {(points > 0 ? "+" : "")}{points:N2} puan düzeltildi.",
            Url.Action("Details", "Customers", new { id = customerId })!);
    }
}
