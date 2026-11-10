using System.Globalization;
using System.Net;
using MektupApp.Domain.Notifications;

namespace MektupApp.Application.Notifications;

/// <summary>Bildirimdeki bağlantılar için sitenin adresi (ör. https://bimektup.net).</summary>
public sealed class NotificationOptions
{
    public const string Section = "Notifications";
    public string SiteUrl { get; set; } = "https://bimektup.net";
    public string BrandName { get; set; } = "BiMektup";
}

/// <summary>Bir bildirimin SMS ve e-posta içerikleri.</summary>
public sealed record NotificationContent(NotificationType Type, string SmsText, string EmailSubject, string EmailText, string EmailHtml);

/// <summary>
/// Bildirim metinleri. SMS'ler kısa tutulur (Türkçe karakterli SMS 155 karakterde bölünür);
/// e-postalar markalı, sade bir HTML şablonla gönderilir. Kullanıcıdan gelen metinler HTML'e kaçışlanarak yazılır.
/// </summary>
public sealed class NotificationTemplates(NotificationOptions options)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private string Site => options.SiteUrl.TrimEnd('/');
    private string Brand => options.BrandName;

    public NotificationContent Welcome(string name, string referralCode) => Build(NotificationType.Welcome,
        sms: $"{Brand}'a hoş geldiniz {FirstName(name)}! İlk mektubunuzu yazmak için: {Site}/mektup-yaz",
        subject: $"{Brand}'a hoş geldiniz 💌",
        heading: $"Hoş geldiniz, {FirstName(name)}!",
        paragraphs:
        [
            "Üyeliğiniz oluşturuldu. Artık sevdiklerinize kağıt kokan mektuplar gönderebilirsiniz: siz yazın, biz basıp zarflayalım ve kayıtlı posta ile gönderelim.",
            $"Referans numaranız: <strong>{Esc(referralCode)}</strong>. Arkadaşlarınız bu numarayla üye olup mektup gönderdikçe siz MektupPuan kazanırsınız."
        ],
        button: ("İlk Mektubumu Yaz", $"{Site}/mektup-yaz"));

    public NotificationContent OrderPaid(string name, string recipientName, decimal amount, Guid letterId) => Build(NotificationType.OrderPaid,
        sms: $"{Brand}: {recipientName} adlı alıcıya mektubunuzun siparişi alındı{(amount > 0 ? $" ({SmsMoney(amount)})" : "")}. İlk iş gününde postaya verilecek.",
        subject: $"Siparişiniz alındı – {recipientName}",
        heading: "Siparişiniz alındı",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, <strong>{Esc(recipientName)}</strong> adlı alıcıya yazdığınız mektubun siparişi alındı{Esc(AmountText(amount))}.",
            "Mektubunuz ilk iş gününde basılıp zarflanacak ve kayıtlı posta ile gönderilecek. Postaya verildiğinde takip kodunuzu ayrıca ileteceğiz."
        ],
        button: ("Siparişi Görüntüle", $"{Site}/mektup/{letterId}"));

    public NotificationContent LetterShipped(string name, string recipientName, string trackingCode) => Build(NotificationType.LetterShipped,
        sms: $"{Brand}: Mektubunuz yola çıktı! Takip kodu: {trackingCode} – {Site}/takip?code={trackingCode}",
        subject: $"Mektubunuz yola çıktı – takip kodu {trackingCode}",
        heading: "Mektubunuz yola çıktı ✉️",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, <strong>{Esc(recipientName)}</strong> adlı alıcıya mektubunuz postaya verildi.",
            $"Takip kodunuz: <strong style=\"font-family:monospace;font-size:16px\">{Esc(trackingCode)}</strong>"
        ],
        button: ("Gönderiyi Takip Et", $"{Site}/takip?code={Uri.EscapeDataString(trackingCode)}"));

    public NotificationContent LetterDelivered(string name, string recipientName, Guid letterId) => Build(NotificationType.LetterDelivered,
        sms: $"{Brand}: Mektubunuz {recipientName} adlı alıcıya teslim edildi. Deneyiminizi değerlendirin: {Site}/mektup/{letterId}#review",
        subject: $"Mektubunuz teslim edildi – {recipientName}",
        heading: "Mektubunuz teslim edildi 💌",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, <strong>{Esc(recipientName)}</strong> adlı alıcıya gönderdiğiniz mektup yerine ulaştı.",
            "Deneyiminizi paylaşırsanız çok seviniriz; yorumunuz diğer müşterilerimize yol gösterir."
        ],
        button: ("Değerlendir", $"{Site}/mektup/{letterId}#review"));

    public NotificationContent OrderCancelled(string name, string recipientName, decimal refundAmount) => Build(NotificationType.OrderCancelled,
        sms: refundAmount > 0
            ? $"{Brand}: {recipientName} adlı alıcıya mektubunuz iptal edildi. {SmsMoney(refundAmount)} iadeniz başlatıldı."
            : $"{Brand}: {recipientName} adlı alıcıya mektubunuz iptal edildi.",
        subject: $"Siparişiniz iptal edildi – {recipientName}",
        heading: "Siparişiniz iptal edildi",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, <strong>{Esc(recipientName)}</strong> adlı alıcıya mektubunuzun siparişi iptal edildi.",
            refundAmount > 0
                ? $"<strong>{Money(refundAmount)}</strong> iadeniz başlatıldı. Tutarın kartınıza yansıması bankanıza göre birkaç iş günü sürebilir. Kullandığınız MektupPuan hesabınıza geri yüklendi."
                : "Kullandığınız MektupPuan varsa hesabınıza geri yüklendi."
        ],
        button: ("Mektuplarım", $"{Site}/mektuplarim"));

    public NotificationContent PointsEarned(string name, decimal points, decimal balance) => Build(NotificationType.PointsEarned,
        sms: $"{Brand}: Tebrikler! Mektubunuz için {points.ToString("N2", Tr)} MektupPuan kazandınız. Bakiye: {balance.ToString("N2", Tr)}",
        subject: $"{points.ToString("N2", Tr)} MektupPuan kazandınız 🎁",
        heading: "MektupPuan kazandınız 🎁",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, postaya verilen mektubunuz için <strong>{points.ToString("N2", Tr)} MektupPuan</strong> hesabınıza yüklendi.",
            $"Güncel bakiyeniz: <strong>{balance.ToString("N2", Tr)} puan</strong>. Puanlarınızı bir sonraki mektubunuzun ödemesinde kullanabilirsiniz."
        ],
        button: ("Puanlarım", $"{Site}/puanlarim"));

    public NotificationContent ReferralPointsEarned(string name, string friendName, decimal points, decimal balance) => Build(NotificationType.ReferralPointsEarned,
        sms: $"{Brand}: Davet ettiğiniz {friendName} mektup gönderdi; {points.ToString("N2", Tr)} MektupPuan kazandınız. Bakiye: {balance.ToString("N2", Tr)}",
        subject: $"Davetiniz kazandırdı: {points.ToString("N2", Tr)} MektupPuan",
        heading: "Davetiniz kazandırdı 🎉",
        paragraphs:
        [
            $"Merhaba {Esc(FirstName(name))}, davet ettiğiniz <strong>{Esc(friendName)}</strong> bir mektup gönderdi ve size <strong>{points.ToString("N2", Tr)} MektupPuan</strong> kazandırdı.",
            $"Güncel bakiyeniz: <strong>{balance.ToString("N2", Tr)} puan</strong>."
        ],
        button: ("Puanlarım", $"{Site}/puanlarim"));

    /// <summary>Yönetim panelinden "test bildirimi gönder" için.</summary>
    public NotificationContent Test() => Build(NotificationType.None,
        sms: $"{Brand} test SMS'i. Bu mesajı aldıysanız SMS ayarlarınız çalışıyor.",
        subject: $"{Brand} test e-postası",
        heading: "Test e-postası",
        paragraphs: ["Bu e-postayı aldıysanız e-posta ayarlarınız çalışıyor."],
        button: ("Siteye Git", Site));

    // ---------------- yardımcılar ----------------

    private NotificationContent Build(NotificationType type, string sms, string subject, string heading,
        string[] paragraphs, (string Text, string Url) button)
    {
        var text = $"{StripTags(heading)}\n\n{string.Join("\n\n", paragraphs.Select(StripTags))}\n\n{button.Text}: {button.Url}\n\n— {Brand}";
        var html = $"""
            <!doctype html>
            <html lang="tr"><body style="margin:0;padding:0;background:#F7F6F0;font-family:Georgia,'Times New Roman',serif;color:#232A3D">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F7F6F0;padding:24px 12px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border:1px solid #E3DFD2;border-radius:12px;overflow:hidden">
                    <tr><td style="background:#26304E;padding:18px 24px;font-size:22px;font-weight:bold;color:#ffffff">
                      <span style="color:#E8A0AC">Bi</span>Mektup<span style="font-size:13px;opacity:.75">.net</span>
                    </td></tr>
                    <tr><td style="padding:28px 24px 8px 24px">
                      <h1 style="margin:0 0 16px 0;font-size:22px;color:#26304E">{Esc(heading)}</h1>
                      {string.Concat(paragraphs.Select(p => $"<p style=\"margin:0 0 14px 0;font-size:15px;line-height:1.6;font-family:Arial,Helvetica,sans-serif\">{p}</p>"))}
                    </td></tr>
                    <tr><td style="padding:8px 24px 28px 24px">
                      <a href="{Esc(button.Url)}" style="display:inline-block;background:#8E2235;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-family:Arial,Helvetica,sans-serif;font-weight:bold;font-size:15px">{Esc(button.Text)}</a>
                    </td></tr>
                    <tr><td style="padding:14px 24px;background:#F7F6F0;font-size:12px;color:#686D7A;font-family:Arial,Helvetica,sans-serif">
                      Bu e-posta {Esc(Brand)} hesabınızdaki işlemler hakkında bilgilendirme amacıyla gönderilmiştir. · <a href="{Esc(Site)}" style="color:#686D7A">{Esc(Site.Replace("https://", "").Replace("http://", ""))}</a>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;
        return new NotificationContent(type, sms, subject, text, html);
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);

    private static string StripTags(string html) =>
        WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", ""));

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;

    private static string Money(decimal amount) => amount.ToString("N2", Tr) + " ₺";

    /// <summary>SMS'te "₺" her cihazda görünmeyebilir.</summary>
    private static string SmsMoney(decimal amount) => amount.ToString("N2", Tr) + " TL";

    private static string AmountText(decimal amount) => amount > 0 ? $" ({Money(amount)})" : "";
}
