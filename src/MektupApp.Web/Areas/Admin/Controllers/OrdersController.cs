using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Letters;
using MektupApp.Domain.Letters;
using MektupApp.Web.Areas.Admin.Models;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

/// <summary>Sipariş (mektup) yönetimi: listeleme, arama, basım / postalama / teslim ve iptal.</summary>
public class OrdersController(IAdminQueries queries, FulfillmentService fulfillment, CarrierShippingService shipping,
    MektupApp.Application.Payments.PaymentService payments) : AdminControllerBase
{
    public async Task<IActionResult> Index(LetterStatus? status, string? q, int page = 1, CancellationToken ct = default)
    {
        var result = await queries.SearchLettersAsync(new LetterSearchCriteria(status, q, Page: page), ct);
        var counts = await queries.CountLettersByStatusAsync(ct);
        SetCarrierInfo();
        return View(new OrdersIndexViewModel { Result = result, Counts = counts, Status = status, Query = q });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var letter = await fulfillment.GetAsync(id, ct);
        var customer = await queries.GetCustomerAsync(letter.CustomerId, ct);
        SetCarrierInfo();
        return View(new OrderDetailsViewModel
        {
            Letter = letter,
            Customer = customer,
            Payments = await payments.ListForLetterAsync(id, ct)
        });
    }

    /// <summary>PTT'de gönderi kaydı açar, barkodu otomatik alır ve mektubu postaya verir.</summary>
    [HttpPost]
    public async Task<IActionResult> ShipWithCarrier(Guid id, CancellationToken ct)
    {
        var barcode = await shipping.ShipAsync(id, ct);
        return BackWith($"{shipping.CarrierName} kaydı açıldı, barkod: {barcode}. Müşteriye SMS gönderildi.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> ShipManyWithCarrier(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) throw new AppException("Lütfen en az bir sipariş seçin.");
        var result = await shipping.ShipManyAsync(ids, ct);
        if (result.Errors.Count > 0)
            TempData["Error"] = $"{result.Errors.Count} mektup gönderilemedi: {string.Join(" · ", result.Errors.Distinct().Take(3))}";
        return BackWith($"{result.Shipped} mektup {shipping.CarrierName}'ye kaydedilip postaya verildi.", Url.Action(nameof(Index))!);
    }

    /// <summary>Mektubun hareketlerini taşıyıcıdan hemen çeker (arka plan senkronizasyonunu beklemeden).</summary>
    [HttpPost]
    public async Task<IActionResult> SyncCarrier(Guid id, CancellationToken ct)
    {
        var r = await shipping.SyncAsync(id, ct);
        var message = r.Error is not null ? $"{shipping.CarrierName} sorgusu: {r.Error}"
            : r.Delivered ? "Mektup teslim edilmiş; durum güncellendi."
            : r.NewMovement ? "Yeni hareket kaydedildi."
            : "Yeni hareket yok.";
        return BackWith(message, DetailsUrl(id));
    }

    private void SetCarrierInfo()
    {
        ViewBag.CarrierName = shipping.CarrierName;
        ViewBag.CarrierLabel = shipping.EnvironmentLabel;
        ViewBag.CarrierIsSimulation = shipping.IsSimulation;
    }

    public async Task<IActionResult> Print(Guid id, CancellationToken ct) =>
        View(await fulfillment.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> StartProduction(Guid id, CancellationToken ct)
    {
        await fulfillment.StartProductionAsync(id, ct);
        return BackWith("Mektup basıma alındı.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> StartProductionMany(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0) throw new AppException("Lütfen en az bir sipariş seçin.");
        var count = await fulfillment.StartProductionManyAsync(ids, ct);
        return BackWith($"{count} mektup basıma alındı.", Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Ship(Guid id, ShipInput input, CancellationToken ct)
    {
        await fulfillment.ShipAsync(id, input.TrackingCode, ct);
        return BackWith("Mektup postaya verildi, müşteriye SMS gönderildi.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        await fulfillment.MarkDeliveredAsync(id, ct);
        return BackWith("Mektup teslim edildi olarak işaretlendi.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new AppException("İptal nedeni girin.");
        await fulfillment.CancelAsync(id, reason, ct);
        return BackWith("Sipariş iptal edildi; ödeme yapıldıysa iade bildirimi gönderildi.", DetailsUrl(id));
    }

    private string DetailsUrl(Guid id) => Url.Action(nameof(Details), new { id })!;
}
