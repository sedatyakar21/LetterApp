using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MektupApp.Application.Payments;
using MektupApp.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class BankTransferSettingsForm
{
    [Display(Name = "Havale / EFT ile ödeme açık")]
    public bool Enabled { get; set; }

    [Range(typeof(decimal), "0", "50", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "İndirim %0 ile %50 arasında olmalıdır.")]
    [Display(Name = "İndirim (%)")]
    public decimal DiscountPercent { get; set; }

    [StringLength(100)]
    [Display(Name = "Banka adı")]
    public string? BankName { get; set; }

    [StringLength(150)]
    [Display(Name = "Hesap sahibi")]
    public string? AccountHolder { get; set; }

    [StringLength(40)]
    [Display(Name = "IBAN")]
    public string? Iban { get; set; }

    [StringLength(500)]
    [Display(Name = "Müşteriye not")]
    public string? Note { get; set; }

    [Range(1, 30, ErrorMessage = "Bekleme süresi 1 ile 30 gün arasında olmalıdır.")]
    [Display(Name = "Bekleme süresi (gün)")]
    public int ValidDays { get; set; } = 3;

    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public IReadOnlyList<PendingBankTransferDto> Pending { get; set; } = [];
}

/// <summary>Havale / EFT ayarları ve onay bekleyen havaleler.</summary>
public class BankTransferController(BankTransferService bankTransfers) : AdminControllerBase
{
    private string AdminName => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "yönetici";

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var s = await bankTransfers.GetAsync(ct);
        return View(new BankTransferSettingsForm
        {
            Enabled = s.Enabled,
            DiscountPercent = s.DiscountRate * 100,
            BankName = s.BankName,
            AccountHolder = s.AccountHolder,
            Iban = s.IbanDisplay,
            Note = s.Note,
            ValidDays = s.ValidDays,
            UpdatedAt = s.UpdatedAt,
            UpdatedBy = s.UpdatedBy,
            Pending = await bankTransfers.ListPendingAsync(ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(BankTransferSettingsForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await bankTransfers.UpdateAsync(new UpdateBankTransferCommand(form.Enabled, form.DiscountPercent / 100m,
                    form.BankName, form.AccountHolder, form.Iban, form.Note, form.ValidDays), AdminName, ct);
                TempData["Success"] = "Havale / EFT ayarları kaydedildi. Yeni indirim bundan sonraki siparişlerde geçerli.";
                return RedirectToAction(nameof(Index));
            }
            catch (DomainException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        form.Pending = await bankTransfers.ListPendingAsync(ct);
        return View(form);
    }
}
