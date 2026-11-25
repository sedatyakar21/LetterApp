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
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;

namespace MektupApp.Web.Controllers;

[Authorize]
public class LettersController(LetterService letters, AccountService accounts, PricingService pricing, PaymentService payments,
    MektupApp.Application.Postcards.PostcardService postcards, MektupApp.Application.Scents.ScentService scents)
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
        var model = new LetterFormViewModel { SenderName = profile?.FullName ?? string.Empty };

        // Cezaevi / askere yazanlar genelde aynı kişiye yazar: o türdeki son mektubun alıcı ve gönderen bilgileri hazır gelsin.
        var prefills = new Dictionary<string, LetterPrefillDto>();
        foreach (var type in new[] { RecipientType.Inmate, RecipientType.Soldier })
            if (await letters.GetPrefillAsync(CustomerId, type, ct) is { } p)
                prefills[((int)type).ToString()] = p;
        if (prefills.TryGetValue(((int)model.RecipientType).ToString(), out var initial))
            model.ApplyPrefill(initial);
        ViewBag.Prefills = prefills;

        return await FormViewAsync(model);
    }

    [HttpGet("mektup/{id:guid}/duzenle")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var letter = await letters.GetAsync(id, CustomerId, ct);
        if (!letter.IsEditable) return RedirectToAction(nameof(Details), new { id });
        return await FormViewAsync(LetterFormViewModel.From(letter));
    }

    /// <param name="next">"save": taslak kaydet, "photos": fotoğraf ekle, "documents": belge ekle, "checkout": ödemeye geç</param>
    [HttpPost("mektup/kaydet")]
    public async Task<IActionResult> Save(LetterFormViewModel model, string next, CancellationToken ct)
    {
        // Taslak, iletişim bilgileri eksikken de kaydedilebilir; ödemeye geçerken hepsi zorunludur.
        if (next != "checkout") IgnoreMissingContacts();
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
            "documents" => RedirectToAction(nameof(Details), null, new { id }, "documents"),
            _ => Saved(id)
        };
    }

    [HttpGet("mektup/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, [FromServices] MektupApp.Application.Reviews.ReviewService reviews, CancellationToken ct)
    {
        var letter = await letters.GetAsync(id, CustomerId, ct);
        // Taslağın her şeyi (mektup, ekstralar, fotoğraf / belge, ödeme) düzenleme ekranında: ayrı mektup sayfası gösterilmez.
        if (letter.IsEditable) return RedirectToAction(nameof(Edit), new { id });
        if (letter.Status == LetterStatus.Delivered)
            ViewBag.Review = await reviews.GetForLetterAsync(id, CustomerId, ct);
        return View(letter);
    }

    [HttpPost("mektup/{id:guid}/fotograf")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> AddPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        if (photo is null) return Fail("Lütfen bir fotoğraf seçin.");
        try
        {
            await using var stream = photo.OpenReadStream();
            await letters.AddPhotoAsync(id, CustomerId, stream, photo.FileName, photo.Length, ct);
        }
        catch (Exception ex) when (WantsJson && ex is DomainException or AppException)
        {
            return Fail(ex.Message);
        }
        if (WantsJson) return await AttachmentsJsonAsync(id, ct);
        TempData["Success"] = "Fotoğraf eklendi.";
        return RedirectToAction(nameof(Details), null, new { id }, "photos");
    }

    [HttpPost("mektup/{id:guid}/fotograf/{photoId:guid}/sil")]
    public async Task<IActionResult> RemovePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        try { await letters.RemovePhotoAsync(id, CustomerId, photoId, ct); }
        catch (Exception ex) when (WantsJson && ex is DomainException or AppException) { return Fail(ex.Message); }
        if (WantsJson) return await AttachmentsJsonAsync(id, ct);
        return RedirectToAction(nameof(Details), null, new { id }, "photos");
    }

    // ---------------- Mektup Yaz ekranından (Ekstralar sekmesi) sayfa içinde yükleme ----------------

    /// <summary>İstek JSON yanıt bekliyor mu (Mektup Yaz ekranındaki sayfa içi yükleme / silme).</summary>
    private bool WantsJson => Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    private IActionResult Fail(string message)
    {
        if (WantsJson) return BadRequest(new { message });
        throw new AppException(message);
    }

    /// <summary>Mektubun güncel fotoğraf ve belge listesi.</summary>
    private async Task<IActionResult> AttachmentsJsonAsync(Guid id, CancellationToken ct)
    {
        var l = await letters.GetAsync(id, CustomerId, ct);
        return Ok(new
        {
            photos = l.Photos.Select(p => new { id = p.Id, name = p.OriginalFileName, url = Url.Action(nameof(Photo), new { id, photoId = p.Id }) }),
            documents = l.Documents.Select(d => new
            {
                id = d.Id, name = d.OriginalFileName, isPdf = d.IsPdf, pageCount = d.PageCount,
                url = Url.Action(nameof(Document), new { id, documentId = d.Id })
            })
        });
    }

    [HttpGet("mektup/{id:guid}/fotograf/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id, Guid photoId, CancellationToken ct)
    {
        var file = await letters.OpenPhotoAsync(id, photoId, CustomerId, User.IsInRole("Admin"), ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.FileName, out var contentType);
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }

    [HttpPost("mektup/{id:guid}/belge")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> AddDocument(Guid id, IFormFile? document, CancellationToken ct)
    {
        if (document is null) return Fail("Lütfen bir belge seçin.");
        try
        {
            await using var stream = document.OpenReadStream();
            await letters.AddDocumentAsync(id, CustomerId, stream, document.FileName, document.Length, ct);
        }
        catch (Exception ex) when (WantsJson && ex is DomainException or AppException)
        {
            return Fail(ex.Message);
        }
        if (WantsJson) return await AttachmentsJsonAsync(id, ct);
        TempData["Success"] = "Belge eklendi. Mektubunuzla birlikte basılıp zarfa konacak.";
        return RedirectToAction(nameof(Details), null, new { id }, "documents");
    }

    [HttpPost("mektup/{id:guid}/belge/{documentId:guid}/sil")]
    public async Task<IActionResult> RemoveDocument(Guid id, Guid documentId, CancellationToken ct)
    {
        try { await letters.RemoveDocumentAsync(id, CustomerId, documentId, ct); }
        catch (Exception ex) when (WantsJson && ex is DomainException or AppException) { return Fail(ex.Message); }
        if (WantsJson) return await AttachmentsJsonAsync(id, ct);
        return RedirectToAction(nameof(Details), null, new { id }, "documents");
    }

    [HttpGet("mektup/{id:guid}/belge/{documentId:guid}")]
    public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken ct)
    {
        var file = await letters.OpenDocumentAsync(id, documentId, CustomerId, User.IsInRole("Admin"), ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.FileName, out var contentType);
        Response.Headers.XContentTypeOptions = "nosniff";
        // Tarayıcıda açılsın (PDF görüntüleyici / resim); kaydedilirse asıl dosya adı kullanılsın.
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.Value.FileName); // Türkçe karakterli adlar için filename* de yazılır
        Response.Headers.ContentDisposition = disposition.ToString();
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
    /// Mektup Yaz ekranının Ödeme sekmesi için fiyat dökümü (JSON): kalemler, kampanya / kupon, MektupPuan ve ödenecek tutar.
    /// </summary>
    [HttpGet("mektup/{id:guid}/odeme/teklif")]
    public async Task<IActionResult> QuoteJson(Guid id, PaymentMethod method, bool usePoints,
        [FromQuery(Name = "kupon")] string? coupon, CancellationToken ct)
    {
        try
        {
            var q = await letters.GetQuoteAsync(id, CustomerId, method, usePoints, ct, coupon);
            if (q.Method == PaymentMethod.Mobile && !q.MobilePaymentAvailable)
                q = await letters.GetQuoteAsync(id, CustomerId, PaymentMethod.Card, usePoints, ct, coupon);
            return Ok(new
            {
                method = q.Method.ToString(),
                q.MobilePaymentAvailable,
                lines = q.Lines.Select(l => new { description = l.Description, amount = l.Amount }),
                campaign = q.Campaign is null ? null : new { title = q.Campaign.Title, discount = q.CampaignDiscount },
                q.CouponCode, q.CouponApplied,
                couponMessage = CouponMessage(q),
                q.UsePoints, q.PointsBalance, q.BalanceValue, q.PointValue, q.PointsUsed, q.PointsDiscount,
                q.Total, q.AmountToPay, q.PointsToEarn,
                pointsToEarnValue = decimal.Round(q.PointsToEarn * q.PointValue, 2),
                q.LoyaltyEnabled, q.ProviderIsSimulation
            });
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Girilen kupon uygulanmadıysa nedeni (ödeme sayfasındaki metinlerle aynı).</summary>
    private static string? CouponMessage(QuoteDto q)
    {
        if (q.CouponCode is null || q.CouponApplied) return null;
        if (!q.CouponRecognized) return "Kod geçerli değil ya da süresi dolmuş.";
        if (q.CouponNotEligible) return "Bu kod size özel koşullara bağlı (yeni üye / doğum günü) ya da kullanım hakkınız doldu.";
        if (q.Campaign is not null) return $"Bu kod siparişinize daha az indirim sağlıyor; daha avantajlı olan \"{q.Campaign.Title}\" kampanyası uygulandı.";
        return "Kod geçerli ancak siparişiniz kampanya koşullarını sağlamıyor (ör. en düşük sepet tutarı).";
    }

    /// <summary>
    /// Ödemeyi başlatır. Tamamı puanla ödeniyorsa hemen tamamlanır; değilse müşteri ödeme formuna
    /// (PayTR iFrame) yönlendirilir ve kesin sonuç PayTR bildirimiyle gelir.
    /// </summary>
    [HttpPost("mektup/{id:guid}/odeme")]
    public async Task<IActionResult> Pay(Guid id, PaymentMethod method, bool usePoints, string? coupon, CancellationToken ct)
    {
        // Mektup Yaz ekranının Ödeme sekmesi (JSON): form sayfa içinde çerçevede açılır; ödeme sonrası dönüş
        // adresi çerçeveden çıkıp sayfanın tamamını sonuca götürür (cerceve=1).
        var embedded = WantsJson;
        StartPaymentResult result;
        try
        {
            result = await payments.StartAsync(new StartPaymentCommand(
                id, CustomerId, method, usePoints,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                oid => Url.Action(nameof(PaymentResult), "Letters", new { id, oid, cerceve = embedded ? 1 : (int?)null }, Request.Scheme)!,
                coupon), ct);
        }
        catch (Exception ex) when (embedded && ex is DomainException or AppException)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (embedded)
        {
            var successMessage = "Siparişiniz alındı! Mektubunuz ilk iş gününde basılıp postaya verilecek.";
            if (result.Completed) TempData["Success"] = successMessage;
            var formUrl = result.FormUrl is { } f && f.StartsWith('/') ? QueryHelpers.AddQueryString(f, "cerceve", "1") : result.FormUrl;
            return Ok(new
            {
                completed = result.Completed,
                merchantOid = result.MerchantOid,
                formUrl,
                redirectUrl = result.Completed ? Url.Action(nameof(Details), new { id }) : null
            });
        }

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
    public async Task<IActionResult> PaymentResult(Guid id, string oid, int? cerceve, CancellationToken ct)
    {
        // Ödeme formu Mektup Yaz ekranında çerçeve içindeyse: çerçeveden çıkıp sonucu sayfanın tamamında göster.
        if (cerceve == 1)
        {
            var target = JsonSerializer.Serialize(Url.Action(nameof(PaymentResult), new { id, oid }));
            return Content($"<!doctype html><meta charset=\"utf-8\"><title>Yönlendiriliyor…</title>" +
                $"<p style=\"font-family:sans-serif\">Ödeme sonucu açılıyor…</p><script>window.top.location.replace({target});</script>",
                "text/html; charset=utf-8");
        }

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
        if (WantsJson) return Ok(new { abandoned = true });
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

    /// <summary>
    /// Mektup sekmesindeki "Devam et": taslağı sayfadan ayrılmadan kaydeder (iletişim bilgileri henüz girilmemiş olabilir).
    /// Yanıt: { id, editUrl } — sayfa sonraki kayıtlarda aynı taslağı günceller.
    /// </summary>
    [HttpPost("mektup/taslak")]
    public async Task<IActionResult> SaveDraft(LetterFormViewModel model, CancellationToken ct)
    {
        IgnoreMissingContacts();
        if (!ModelState.IsValid)
            return BadRequest(new { message = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Taslak kaydedilemedi." });
        try
        {
            var id = await letters.SaveDraftAsync(model.ToCommand(CustomerId), ct);
            return Ok(new { id, editUrl = Url.Action(nameof(Edit), new { id }) });
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Taslak kaydında alıcı / gönderen alanlarının "zorunlu" hatalarını yok sayar (uzunluk vb. kurallar kalır).</summary>
    private void IgnoreMissingContacts()
    {
        string[] prefixes = [nameof(LetterFormViewModel.SenderName), nameof(LetterFormViewModel.RecipientName),
            nameof(LetterFormViewModel.SenderAddress) + ".", nameof(LetterFormViewModel.RecipientAddress) + "."];
        foreach (var (key, entry) in ModelState.Where(e => prefixes.Any(p => e.Key.StartsWith(p, StringComparison.Ordinal))).ToList())
        {
            var required = entry.Errors.Where(e => e.ErrorMessage.Contains("zorunlu", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var error in required) entry.Errors.Remove(error);
            if (entry.Errors.Count == 0) ModelState.MarkFieldValid(key);
        }
    }

    private IActionResult Saved(Guid id)
    {
        TempData["Success"] = "Taslak kaydedildi. Dilediğiniz zaman kaldığınız yerden devam edebilirsiniz.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task<ViewResult> FormViewAsync(LetterFormViewModel model)
    {
        // Fotoğraf / belge listesi formla gönderilmez: hata sonrası yeniden gösterimde kayıttan okunur.
        if (model.Id is { } letterId && model.Photos.Count == 0 && model.Documents.Count == 0)
        {
            var current = await letters.GetAsync(letterId, CustomerId, HttpContext.RequestAborted);
            model.Photos = current.Photos;
            model.Documents = current.Documents;
            model.PhotoCount = current.Photos.Count;
            model.DocumentCount = current.Documents.Count;
        }
        ViewBag.Prices = await pricing.GetCurrentAsync(HttpContext.RequestAborted);
        ViewBag.Postcards = await postcards.ListActiveAsync(HttpContext.RequestAborted);
        ViewBag.Scents = await scents.ListActiveAsync(HttpContext.RequestAborted);
        ViewBag.Weights = await HttpContext.RequestServices.GetRequiredService<WeightService>().GetCurrentAsync(HttpContext.RequestAborted);
        return View("Write", model);
    }
}
