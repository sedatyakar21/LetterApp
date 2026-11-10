using MektupApp.Application.Abstractions;
using MektupApp.Application.Letters;
using MektupApp.Application.Payments;
using MektupApp.Application.Reviews;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Payments;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MektupApp.Web.Api;

// ---------------- İstek / yanıt modelleri ----------------

public sealed record LetterSaveRequest(
    string? SenderName,
    AddressDto? SenderAddress,
    string? RecipientName,
    RecipientType RecipientType,
    string? RecipientDetail,
    AddressDto? RecipientAddress,
    string? Content,
    PaperColor Paper,
    EnvelopeColor Envelope,
    LetterFont Font,
    InkColor Ink,
    Scent Scent,
    PostcardCategory? PostcardCategory,
    int? PostcardDesign);

public sealed record LetterSummaryApiDto(Guid Id, string RecipientName, string RecipientType, string Status, string StatusLabel,
    string Preview, DateTime UpdatedAt, string? TrackingCode);

public sealed record PhotoApiDto(Guid Id, string FileName, string Url);

public sealed record ReviewMineDto(int Rating, string Comment, string Status, string StatusLabel, string? RejectionReason);

public sealed record PendingPaymentApiDto(string MerchantOid, decimal Amount, string? FormUrl, bool Simulation, DateTime CreatedAt);

public sealed record LetterApiDto(
    Guid Id,
    string SenderName,
    AddressDto SenderAddress,
    string RecipientName,
    string RecipientType,
    string? RecipientDetail,
    AddressDto RecipientAddress,
    string Content,
    string Paper,
    string Envelope,
    string Font,
    string Ink,
    string Scent,
    string? PostcardCategory,
    int? PostcardDesign,
    string Status,
    string StatusLabel,
    int PageCount,
    decimal? PaidAmount,
    string? PaymentMethod,
    string? TrackingCode,
    string? Carrier,
    string? CarrierLastMovement,
    DateTime? CarrierLastMovementAt,
    IReadOnlyList<PhotoApiDto> Photos,
    IReadOnlyList<StatusChangeApiDto> History,
    bool IsEditable,
    bool CanCancel,
    bool CanDelete,
    bool CanChangeAddress,
    decimal PointsUsed,
    decimal PointsDiscount,
    decimal PointsToEarn,
    string? CampaignTitle,
    decimal CampaignDiscount,
    DateTime CreatedAt,
    bool CanReview,
    ReviewMineDto? Review,
    PendingPaymentApiDto? PendingPayment);

public sealed record QuoteLineDto(string Description, decimal Amount);

public sealed record QuoteApiDto(
    string Method,
    IReadOnlyList<QuoteLineDto> Lines,
    decimal Total,
    decimal CampaignDiscount,
    string? CampaignTitle,
    bool UsePoints,
    decimal PointsBalance,
    decimal PointsUsed,
    decimal PointsDiscount,
    decimal BalanceValue,
    decimal AmountToPay,
    decimal PointsToEarn,
    bool LoyaltyEnabled,
    bool MobilePaymentAvailable,
    string ProviderLabel,
    bool ProviderIsSimulation,
    string? CouponCode,
    bool CouponApplied,
    string? CouponMessage);

public sealed record PayRequest(PaymentMethod Method, bool UsePoints, string? Coupon);

/// <param name="FormUrl">Ödeme sağlayıcısının formu (uygulama içi tarayıcıda açılır). Simülasyonda null.</param>
/// <param name="ReturnUrl">Form bitince yönlenilen adres; uygulama bu adrese gelince tarayıcıyı kapatıp sonucu sorgular.</param>
public sealed record PayResponse(bool Completed, string MerchantOid, string? FormUrl, bool Simulation, string ReturnUrl);

public sealed record PaymentStatusApiDto(string MerchantOid, Guid LetterId, string Status, string StatusLabel, decimal Amount, string? FailureReason);

public sealed record SimulateRequest(bool Success);

public sealed record ReviewRequest(int Rating, string? Comment);

// ---------------- Denetleyici ----------------

/// <summary>Mektuplarım: taslak, fotoğraf, ödeme, iptal, adres değişikliği ve yorum.</summary>
[Route("api/mobile/letters")]
public sealed class MobileLettersController(
    LetterService letters,
    PaymentService payments,
    ReviewService reviews,
    IPaymentProvider provider) : MobileApiController
{
    [HttpGet]
    public async Task<IReadOnlyList<LetterSummaryApiDto>> List(CancellationToken ct) =>
        (await letters.ListMineAsync(CustomerId, ct)).Select(l => new LetterSummaryApiDto(
            l.Id, l.RecipientName, l.RecipientType.ToString(), l.Status.ToString(), l.Status.DisplayName(),
            l.Preview, l.UpdatedAt, l.TrackingCode)).ToList();

    [HttpGet("{id:guid}")]
    public Task<LetterApiDto> Get(Guid id, CancellationToken ct) => DetailsAsync(id, ct);

    [HttpPost]
    public async Task<LetterApiDto> Create(LetterSaveRequest req, CancellationToken ct) =>
        await DetailsAsync(await letters.SaveDraftAsync(ToCommand(null, req), ct), ct);

    [HttpPut("{id:guid}")]
    public async Task<LetterApiDto> Update(Guid id, LetterSaveRequest req, CancellationToken ct) =>
        await DetailsAsync(await letters.SaveDraftAsync(ToCommand(id, req), ct), ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await letters.DeleteAsync(id, CustomerId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<LetterApiDto> Cancel(Guid id, CancellationToken ct)
    {
        await letters.CancelAsync(id, CustomerId, ct);
        return await DetailsAsync(id, ct);
    }

    [HttpPut("{id:guid}/address")]
    public async Task<LetterApiDto> ChangeAddress(Guid id, AddressDto address, CancellationToken ct)
    {
        await letters.ChangeRecipientAddressAsync(id, CustomerId, Clean(address, "Alıcı"), ct);
        return await DetailsAsync(id, ct);
    }

    // ---------------- Fotoğraflar ----------------

    [HttpPost("{id:guid}/photos")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<LetterApiDto> AddPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        if (photo is null) throw new AppException("Lütfen bir fotoğraf seçin.");
        await using (var stream = photo.OpenReadStream())
            await letters.AddPhotoAsync(id, CustomerId, stream, photo.FileName, photo.Length, ct);
        return await DetailsAsync(id, ct);
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    public async Task<LetterApiDto> RemovePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await letters.RemovePhotoAsync(id, CustomerId, photoId, ct);
        return await DetailsAsync(id, ct);
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id, Guid photoId, CancellationToken ct)
    {
        var file = await letters.OpenPhotoAsync(id, photoId, CustomerId, isAdmin: false, ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.FileName, out var contentType);
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }

    // ---------------- Ödeme ----------------

    [HttpGet("{id:guid}/quote")]
    public async Task<QuoteApiDto> Quote(Guid id, PaymentMethod method = PaymentMethod.Card, bool usePoints = false,
        string? coupon = null, CancellationToken ct = default)
    {
        var q = await letters.GetQuoteAsync(id, CustomerId, method, usePoints, ct, coupon);
        string? couponMessage = q.CouponCode switch
        {
            null => null,
            _ when q.CouponApplied => null,
            _ when q.CouponNotEligible => $"\"{q.CouponCode}\" kodu bu hesap için kullanılamıyor (kampanya koşulları veya kullanım sınırı).",
            _ when q.CouponRecognized => $"\"{q.CouponCode}\" kodu geçerli, ancak bu siparişte daha avantajlı bir kampanya uygulandı veya koşullar sağlanmadı.",
            _ => $"\"{q.CouponCode}\" geçerli bir kupon kodu değil."
        };
        return new QuoteApiDto(q.Method.ToString(), q.Lines.Select(l => new QuoteLineDto(l.Description, l.Amount)).ToList(),
            q.Total, q.CampaignDiscount, q.Campaign?.Title, q.UsePoints, q.PointsBalance, q.PointsUsed, q.PointsDiscount,
            q.BalanceValue, q.AmountToPay, q.PointsToEarn, q.LoyaltyEnabled, q.MobilePaymentAvailable, q.ProviderLabel,
            q.ProviderIsSimulation, q.CouponCode, q.CouponApplied, couponMessage);
    }

    /// <summary>
    /// Ödemeyi başlatır. Tutar 0 ise (puan / kampanya) hemen tamamlanır. Değilse uygulama <c>formUrl</c>'i
    /// uygulama içi tarayıcıda açar; kesin sonuç sağlayıcının sunucu bildirimiyle gelir, uygulama durumu sorgular.
    /// </summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<PayResponse> Pay(Guid id, PayRequest req, CancellationToken ct)
    {
        var returnUrl = $"{Request.Scheme}://{Request.Host}{MobileApi.PaymentReturnPath}";
        var result = await payments.StartAsync(new StartPaymentCommand(
            id, CustomerId, req.Method, req.UsePoints,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            oid => $"{returnUrl}?oid={Uri.EscapeDataString(oid)}",
            string.IsNullOrWhiteSpace(req.Coupon) ? null : req.Coupon), ct);
        return new PayResponse(result.Completed, result.MerchantOid, ExternalFormUrl(result.FormUrl),
            !result.Completed && provider.IsSimulation, returnUrl);
    }

    /// <summary>Ödemeden vazgeç: mektup tekrar düzenlenebilir, ayrılan puan iade edilir.</summary>
    [HttpPost("{id:guid}/pay/abandon")]
    public async Task<LetterApiDto> Abandon(Guid id, CancellationToken ct)
    {
        await payments.AbandonAsync(id, CustomerId, ct);
        return await DetailsAsync(id, ct);
    }

    [HttpGet("payments/{oid}")]
    public async Task<ActionResult<PaymentStatusApiDto>> PaymentStatus(string oid, CancellationToken ct)
    {
        var s = await payments.GetStatusAsync(oid, CustomerId, ct);
        if (s is null) return NotFound(new ApiError("Ödeme bulunamadı."));
        return new PaymentStatusApiDto(s.MerchantOid, s.LetterId, s.Status.ToString(), s.Status.DisplayName(), s.Amount, s.FailureReason);
    }

    /// <summary>Yalnızca simülasyon sağlayıcısında: bankanın onay / ret bildirimini taklit eder.</summary>
    [HttpPost("payments/{oid}/simulate")]
    public async Task<ActionResult<PaymentStatusApiDto>> Simulate(string oid, SimulateRequest req, CancellationToken ct)
    {
        if (!provider.IsSimulation) return NotFound(new ApiError("Simülasyon kapalı."));
        var s = await payments.GetStatusAsync(oid, CustomerId, ct);
        if (s is null) return NotFound(new ApiError("Ödeme bulunamadı."));
        await payments.CompleteAsync(new PaymentNotification(oid, req.Success, s.Amount,
            req.Success ? null : "Simülasyon: banka işlemi reddetti (kod 0)", IsTest: true), ct);
        return await PaymentStatus(oid, ct);
    }

    // ---------------- Yorum ----------------

    [HttpPost("{id:guid}/review")]
    public async Task<LetterApiDto> Review(Guid id, ReviewRequest req, CancellationToken ct)
    {
        await reviews.SubmitAsync(CustomerId, id, req.Rating, req.Comment ?? string.Empty, ct);
        return await DetailsAsync(id, ct);
    }

    // ---------------- yardımcılar ----------------

    private async Task<LetterApiDto> DetailsAsync(Guid id, CancellationToken ct)
    {
        var l = await letters.GetAsync(id, CustomerId, ct);
        ReviewMineDto? review = null;
        if (l.Status == LetterStatus.Delivered && await reviews.GetForLetterAsync(id, CustomerId, ct) is { } r)
            review = new ReviewMineDto(r.Rating, r.Comment, r.Status.ToString(), r.Status.DisplayName(), r.RejectionReason);

        PendingPaymentApiDto? pending = null;
        if (l.Status == LetterStatus.AwaitingPayment && await payments.GetPendingForLetterAsync(id, CustomerId, ct) is { } p)
            pending = new PendingPaymentApiDto(p.MerchantOid, p.Amount, ExternalFormUrl(p.FormUrl), provider.IsSimulation, p.CreatedAt);

        return new LetterApiDto(
            l.Id, l.SenderName, l.SenderAddress, l.RecipientName, l.RecipientType.ToString(), l.RecipientDetail, l.RecipientAddress,
            l.Content, l.Style.Paper.ToString(), l.Style.Envelope.ToString(), l.Style.Font.ToString(), l.Style.Ink.ToString(),
            l.Scent.ToString(), l.Postcard?.Category.ToString(), l.Postcard?.Design,
            l.Status.ToString(), l.Status.DisplayName(), l.PageCount, l.PaidAmount, l.PaymentMethod?.ToString(), l.TrackingCode,
            l.Carrier, l.CarrierLastMovement, l.CarrierLastMovementAt,
            l.Photos.Select(ph => new PhotoApiDto(ph.Id, ph.OriginalFileName, $"/api/mobile/letters/{l.Id}/photos/{ph.Id}")).ToList(),
            l.History.Select(ApiMapping.ToApi).ToList(),
            l.IsEditable, l.CanCancel, l.CanDelete, l.CanChangeAddress,
            l.PointsUsed, l.PointsDiscount, l.PointsToEarn, l.CampaignTitle, l.CampaignDiscount, l.CreatedAt,
            l.Status == LetterStatus.Delivered && review is null, review, pending);
    }

    /// <summary>Simülasyon formu sitenin kendi sayfasıdır (çerez ister); uygulama onun yerine kendi ekranını gösterir.</summary>
    private static string? ExternalFormUrl(string? formUrl) =>
        formUrl is null || formUrl.StartsWith('/') ? null : formUrl;

    private SaveLetterCommand ToCommand(Guid? id, LetterSaveRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.SenderName)) throw new AppException("Gönderen adı zorunludur.");
        if (string.IsNullOrWhiteSpace(r.RecipientName)) throw new AppException("Alıcı adı zorunludur.");
        return new SaveLetterCommand(id, CustomerId, r.SenderName.Trim(), Clean(r.SenderAddress, "Gönderen"),
            r.RecipientName.Trim(), r.RecipientType, string.IsNullOrWhiteSpace(r.RecipientDetail) ? null : r.RecipientDetail.Trim(),
            Clean(r.RecipientAddress, "Alıcı"), r.Content, r.Paper, r.Envelope, r.Font, r.Ink, r.Scent,
            r.PostcardCategory, r.PostcardCategory is null ? null : r.PostcardDesign);
    }

    /// <summary>Web formundaki doğrulamanın aynısı: boş alan ve 5 haneli posta kodu.</summary>
    private static AddressDto Clean(AddressDto? a, string who)
    {
        if (a is null || string.IsNullOrWhiteSpace(a.Line) || string.IsNullOrWhiteSpace(a.District) || string.IsNullOrWhiteSpace(a.City))
            throw new AppException($"Lütfen {who.ToLowerInvariant()} adresini eksiksiz girin (adres, ilçe, il).");
        var postal = (a.PostalCode ?? "").Trim();
        if (postal.Length != 5 || !postal.All(char.IsAsciiDigit))
            throw new AppException($"{who} posta kodu 5 haneli olmalıdır.");
        return new AddressDto(a.Line.Trim(), a.District.Trim(), a.City.Trim(), postal);
    }
}

/// <summary>Ödeme formundan sonra dönülen sayfa. Uygulama bu adresi yakalayıp kapatır; tarayıcıda açılırsa bilgi verir.</summary>
[AllowAnonymous]
public sealed class MobilePaymentReturnController : Controller
{
    [HttpGet(MobileApi.PaymentReturnPath)]
    public ContentResult Index() => Content(
        "<!doctype html><html lang=\"tr\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        "<title>BiMektup</title><body style=\"font-family:system-ui;text-align:center;padding:3rem 1rem;color:#232A3D;background:#F7F6F0\">" +
        "<h1 style=\"font-size:1.3rem\">Ödeme işlemi tamamlandı</h1><p>BiMektup uygulamasına dönebilirsiniz.</p></body></html>",
        "text/html; charset=utf-8");
}
