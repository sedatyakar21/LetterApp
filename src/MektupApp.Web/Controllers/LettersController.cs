using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Application.Letters;
using MektupApp.Application.Payments;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Web.Infrastructure;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MektupApp.Web.Controllers;

[Authorize]
public class LettersController(LetterService letters, AccountService accounts, PricingService pricing, PaymentService payments)
    : Controller
{
    private Guid CustomerId => User.GetCustomerId();

    [HttpGet("mektuplarim")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await letters.ListMineAsync(CustomerId, ct));

    [HttpGet("mektup-yaz")]
    public async Task<IActionResult> Write(CancellationToken ct)
    {
        var profile = await accounts.GetProfileAsync(CustomerId, ct);
        return await FormViewAsync(new LetterFormViewModel { SenderName = profile?.FullName ?? string.Empty });
    }

    [HttpGet("mektup/{id:guid}/duzenle")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var letter = await letters.GetAsync(id, CustomerId, ct);
        if (!letter.IsEditable) return RedirectToAction(nameof(Details), new { id });
        return await FormViewAsync(LetterFormViewModel.From(letter));
    }

    /// <param name="next">"save": taslak kaydet, "photos": fotoğraf ekle, "checkout": ödemeye geç</param>
    [HttpPost("mektup/kaydet")]
    public async Task<IActionResult> Save(LetterFormViewModel model, string next, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await FormViewAsync(model);

        Guid id;
        try
        {
            id = await letters.SaveDraftAsync(model.ToCommand(CustomerId), ct);
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            // Formu kullanıcının yazdıklarıyla tekrar göster; mektup metni kaybolmasın.
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(model);
        }

        return next switch
        {
            "checkout" => RedirectToAction(nameof(Checkout), new { id }),
            "photos" => RedirectToAction(nameof(Details), null, new { id }, "photos"),
            _ => Saved(id)
        };
    }

    [HttpGet("mektup/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, [FromServices] MektupApp.Application.Reviews.ReviewService reviews, CancellationToken ct)
    {
        var letter = await letters.GetAsync(id, CustomerId, ct);
        if (letter.Status == LetterStatus.Delivered)
            ViewBag.Review = await reviews.GetForLetterAsync(id, CustomerId, ct);
        return View(letter);
    }

    [HttpPost("mektup/{id:guid}/fotograf")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> AddPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        if (photo is null) throw new AppException("Lütfen bir fotoğraf seçin.");
        await using var stream = photo.OpenReadStream();
        await letters.AddPhotoAsync(id, CustomerId, stream, photo.FileName, photo.Length, ct);
        TempData["Success"] = "Fotoğraf eklendi.";
        return RedirectToAction(nameof(Details), null, new { id }, "photos");
    }

    [HttpPost("mektup/{id:guid}/fotograf/{photoId:guid}/sil")]
    public async Task<IActionResult> RemovePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await letters.RemovePhotoAsync(id, CustomerId, photoId, ct);
        return RedirectToAction(nameof(Details), null, new { id }, "photos");
    }

    [HttpGet("mektup/{id:guid}/fotograf/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id, Guid photoId, CancellationToken ct)
    {
        var file = await letters.OpenPhotoAsync(id, photoId, CustomerId, User.IsInRole("Admin"), ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.FileName, out var contentType);
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }

    [HttpGet("mektup/{id:guid}/odeme")]
    public async Task<IActionResult> Checkout(Guid id, PaymentMethod method, bool usePoints,
        [FromQuery(Name = "kupon")] string? coupon, CancellationToken ct)
    {
        var letter = await letters.GetAsync(id, CustomerId, ct);

        // Başlamış bir ödeme varsa: ödeme sayfasına dönme veya vazgeçme seçenekleri.
        if (letter.Status == LetterStatus.AwaitingPayment)
            return View("PendingPayment", new PendingPaymentViewModel
            {
                Letter = letter,
                Payment = await payments.GetPendingForLetterAsync(id, CustomerId, ct)
            });

        if (!letter.IsEditable) return RedirectToAction(nameof(Details), new { id });
        try
        {
            var quote = await letters.GetQuoteAsync(id, CustomerId, method, usePoints, ct, coupon);
            if (quote.Method == PaymentMethod.Mobile && !quote.MobilePaymentAvailable)
                return RedirectToAction(nameof(Checkout), new { id, method = PaymentMethod.Card, usePoints, kupon = quote.CouponCode });
            return View(new CheckoutViewModel { Letter = letter, Quote = quote });
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    /// <summary>
    /// Ödemeyi başlatır. Tamamı puanla ödeniyorsa hemen tamamlanır; değilse müşteri ödeme formuna
    /// (PayTR iFrame) yönlendirilir ve kesin sonuç PayTR bildirimiyle gelir.
    /// </summary>
    [HttpPost("mektup/{id:guid}/odeme")]
    public async Task<IActionResult> Pay(Guid id, PaymentMethod method, bool usePoints, string? coupon, CancellationToken ct)
    {
        var result = await payments.StartAsync(new StartPaymentCommand(
            id, CustomerId, method, usePoints,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            oid => Url.Action(nameof(PaymentResult), "Letters", new { id, oid }, Request.Scheme)!,
            coupon), ct);

        if (result.Completed)
        {
            TempData["Success"] = usePoints
                ? "Ödemeniz MektupPuan ile alındı! Mektubunuz ilk iş gününde basılıp postaya verilecek."
                : "Siparişiniz kampanya indirimiyle ücretsiz tamamlandı! Mektubunuz ilk iş gününde basılıp postaya verilecek.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Simülasyon formu uygulamanın kendi sayfasıdır; PayTR formu iFrame içinde açılır.
        return result.FormUrl!.StartsWith('/')
            ? Redirect(result.FormUrl)
            : RedirectToAction(nameof(SecurePayment), new { id, oid = result.MerchantOid });
    }

    /// <summary>PayTR ödeme formunu (iFrame) gösterir.</summary>
    [HttpGet("mektup/{id:guid}/odeme/guvenli")]
    public async Task<IActionResult> SecurePayment(Guid id, string oid, CancellationToken ct)
    {
        var status = await payments.GetStatusAsync(oid, CustomerId, ct);
        if (status is null || status.LetterId != id) return NotFound();
        if (status.Status != Domain.Payments.PaymentStatus.Pending || status.FormUrl is null)
            return RedirectToAction(nameof(PaymentResult), new { id, oid });
        if (status.FormUrl.StartsWith('/')) return Redirect(status.FormUrl);
        return View(status);
    }

    /// <summary>
    /// Ödeme sonrası müşterinin döndüğü sayfa. Sonuç PayTR bildirimiyle kesinleşir;
    /// bildirim gelene kadar sayfa kendini yeniler.
    /// </summary>
    [HttpGet("mektup/{id:guid}/odeme/sonuc")]
    public async Task<IActionResult> PaymentResult(Guid id, string oid, CancellationToken ct)
    {
        var status = await payments.GetStatusAsync(oid, CustomerId, ct);
        if (status is null || status.LetterId != id) return NotFound();

        if (status.Status == Domain.Payments.PaymentStatus.Succeeded)
        {
            TempData["Success"] = "Ödemeniz alındı! Mektubunuz ilk iş gününde basılıp postaya verilecek.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(status);
    }

    /// <summary>Ödemeden vazgeç: mektup tekrar düzenlenebilir, ayrılan puan iade edilir.</summary>
    [HttpPost("mektup/{id:guid}/odeme/vazgec")]
    public async Task<IActionResult> AbandonPayment(Guid id, CancellationToken ct)
    {
        await payments.AbandonAsync(id, CustomerId, ct);
        TempData["Success"] = "Ödemeden vazgeçildi. Mektubunuzu düzenleyebilir veya tekrar ödeyebilirsiniz.";
        return RedirectToAction(nameof(Checkout), new { id });
    }

    [HttpPost("mektup/{id:guid}/iptal")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await letters.CancelAsync(id, CustomerId, ct);
        TempData["Success"] = "Mektubunuz iptal edildi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("mektup/{id:guid}/sil")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await letters.DeleteAsync(id, CustomerId, ct);
        TempData["Success"] = "Mektup silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mektup/{id:guid}/adres")]
    public async Task<IActionResult> ChangeAddress(Guid id, AddressInput address, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            throw new AppException("Lütfen adres bilgilerini eksiksiz girin.");
        await letters.ChangeRecipientAddressAsync(id, CustomerId, address.ToDto(), ct);
        TempData["Success"] = "Alıcı adresi güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult Saved(Guid id)
    {
        TempData["Success"] = "Taslak kaydedildi. Dilediğiniz zaman kaldığınız yerden devam edebilirsiniz.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<ViewResult> FormViewAsync(LetterFormViewModel model)
    {
        ViewBag.Prices = await pricing.GetCurrentAsync(HttpContext.RequestAborted);
        return View("Write", model);
    }
}
