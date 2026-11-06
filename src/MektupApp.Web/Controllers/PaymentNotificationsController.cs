using MektupApp.Application.Abstractions;
using MektupApp.Application.Payments;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

/// <summary>
/// Ödeme sağlayıcısından gelen sunucudan sunucuya bildirimler ve geliştirme için simülasyon ödeme sayfası.
/// </summary>
public class PaymentNotificationsController(
    PaymentService payments,
    IPaymentProvider provider,
    ILogger<PaymentNotificationsController> logger) : Controller
{
    /// <summary>
    /// PayTR "Bildirim URL" (Mağaza Paneli → Destek &amp; Kurulum → Ayarlar): https://alanadiniz/odeme/paytr/bildirim
    /// Kurallar (PayTR): giriş istemez, oturum kullanmaz, hash doğrulanır, yanıt sadece "OK" metnidir;
    /// aynı bildirim birden fazla gelebilir, tekrarlar için de "OK" dönülür.
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("odeme/paytr/bildirim")]
    public async Task<IActionResult> PayTrNotification(CancellationToken ct)
    {
        var form = (await Request.ReadFormAsync(ct)).ToDictionary(k => k.Key, v => v.Value.ToString());
        var notification = provider.VerifyNotification(form);
        if (notification is null)
        {
            // Hash tutmuyor: istek PayTR'den gelmiyor olabilir. İşlem yapma; PayTR'ye de "OK" deme.
            return BadRequest("PAYTR notification failed: bad hash");
        }

        PaymentCompletion result;
        try
        {
            result = await payments.CompleteAsync(notification, ct);
        }
        catch (Domain.Common.DomainException ex)
        {
            // Ör. tahsil edilen tutar beklenenden az: otomatik işlem yapılmaz, elle incelenmeli.
            // "OK" dönülür ki PayTR aynı hatalı bildirimi sürekli tekrar etmesin.
            logger.LogCritical(ex, "PayTR bildirimi işlenemedi, elle incelenmeli: {Oid}", notification.MerchantOid);
            return Content("OK", "text/plain");
        }
        logger.LogInformation("PayTR bildirimi: {Oid} başarı={Success} → {Result}", notification.MerchantOid, notification.Success, result);
        if (result == PaymentCompletion.LateSuccessRefundFailed)
            logger.LogError("Geç gelen ödeme iade edilemedi, elle iade gerekiyor: {Oid}", notification.MerchantOid);

        return Content("OK", "text/plain");
    }

    // ---------------- Simülasyon (sadece Payment:Provider = Simulation) ----------------

    [Authorize]
    [HttpGet("odeme/simulasyon/{oid}")]
    public async Task<IActionResult> Simulation(string oid, CancellationToken ct)
    {
        if (!provider.IsSimulation) return NotFound();
        var status = await payments.GetStatusAsync(oid, User.GetCustomerId(), ct);
        return status is null ? NotFound() : View(status);
    }

    [Authorize]
    [HttpPost("odeme/simulasyon/{oid}")]
    public async Task<IActionResult> Simulation(string oid, string result, CancellationToken ct)
    {
        if (!provider.IsSimulation) return NotFound();
        var status = await payments.GetStatusAsync(oid, User.GetCustomerId(), ct);
        if (status is null) return NotFound();

        // PayTR'nin sunucuya göndereceği bildirimi taklit et (uygulama içi; dışarıdan bildirim kabul edilmez).
        var success = result == "success";
        await payments.CompleteAsync(new PaymentNotification(oid, success, status.Amount,
            success ? null : "Simülasyon: banka işlemi reddetti (kod 0)", IsTest: true), ct);

        return RedirectToAction("PaymentResult", "Letters", new { id = status.LetterId, oid });
    }
}
