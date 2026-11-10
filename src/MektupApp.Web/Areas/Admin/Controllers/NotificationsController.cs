using MektupApp.Application.Admin;
using MektupApp.Application.Notifications;
using MektupApp.Domain.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class NotificationsIndexViewModel
{
    public required NotificationSettingsDto Settings { get; init; }
    public required PagedResult<NotificationRowDto> History { get; init; }
    public NotificationChannel? Channel { get; init; }
    public NotificationStatus? Status { get; init; }
}

/// <summary>SMS / e-posta bildirim ayarları, gönderim geçmişi ve test gönderimi.</summary>
public class NotificationsController(NotificationAdminService notifications) : AdminControllerBase
{
    private const int PageSize = 25;

    /// <summary>Ayarlarda gösterilen türler (sıra ekrandaki sıradır).</summary>
    public static readonly NotificationType[] Types =
    [
        NotificationType.Welcome, NotificationType.OrderPaid, NotificationType.LetterShipped, NotificationType.LetterDelivered,
        NotificationType.OrderCancelled, NotificationType.PointsEarned, NotificationType.ReferralPointsEarned
    ];

    public async Task<IActionResult> Index(NotificationChannel? kanal, NotificationStatus? durum, int page = 1, CancellationToken ct = default) =>
        View(new NotificationsIndexViewModel
        {
            Settings = await notifications.GetSettingsAsync(ct),
            History = await notifications.SearchAsync(kanal, durum, page, PageSize, ct),
            Channel = kanal,
            Status = durum
        });

    /// <param name="sms">İşaretli SMS türleri (bayrak değerleri).</param>
    /// <param name="email">İşaretli e-posta türleri.</param>
    [HttpPost]
    public async Task<IActionResult> Settings(int[]? sms, int[]? email, CancellationToken ct)
    {
        await notifications.UpdateSettingsAsync(Combine(sms), Combine(email), ct);
        return BackWith("Bildirim ayarları kaydedildi.", Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Test(NotificationChannel channel, string? recipient, CancellationToken ct)
    {
        await notifications.SendTestAsync(channel, recipient ?? string.Empty, ct);
        return BackWith($"Test {(channel == NotificationChannel.Sms ? "SMS'i" : "e-postası")} kuyruğa eklendi; birkaç saniye içinde gönderilir.",
            Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        await notifications.RetryAsync(id, ct);
        return BackWith("Bildirim yeniden kuyruğa alındı.", Url.Action(nameof(Index))!);
    }

    /// <summary>E-postanın HTML halini gösterir. Sayfa betik çalıştıramaz (CSP sandbox).</summary>
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        var html = await notifications.GetEmailHtmlAsync(id, ct);
        if (html is null) return NotFound();
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src * data:; style-src 'unsafe-inline'";
        return Content(html, "text/html; charset=utf-8");
    }

    // Yalnızca tanımlı türler kabul edilir; formdan gelen başka sayılar yok sayılır.
    private static NotificationType Combine(int[]? values) =>
        (values ?? []).Select(v => (NotificationType)v).Where(Types.Contains).Aggregate(NotificationType.None, (a, t) => a | t);
}
