using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MektupApp.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Carriers;

/// <summary>
/// PTT web servisleri adapter'ı (SOAP 1.1).
///  - Gönderi kaydı: PttVeriYukleme[Test] → kabulEkle2   (namespace http://kabul.ptt.gov.tr)
///  - Gönderi takip: GonderiTakipV2[Test] → gonderiSorgu2 (namespace http://takip.ptt.gov.tr)
/// Eleman adları ve sıraları pttws.ptt.gov.tr üzerindeki WSDL'lerden alınmıştır (Kasım 2026).
/// Servis Axis2 tabanlıdır ve eleman sırasına duyarlıdır: sırayı WSDL'deki gibi koruyun.
/// </summary>
public sealed class PttPostalCarrier(HttpClient http, IOptions<PttOptions> options, ILogger<PttPostalCarrier> logger)
    : IPostalCarrier
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Kabul = "http://kabul.ptt.gov.tr";
    private static readonly XNamespace KabulXsd = "http://kabul.ptt.gov.tr/xsd";
    private static readonly XNamespace Takip = "http://takip.ptt.gov.tr";
    private static readonly XNamespace TakipXsd = "http://takip.ptt.gov.tr/xsd";

    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly PttOptions _o = options.Value;

    public string Name => "PTT";
    public string EnvironmentLabel => _o.UseTestEnvironment ? "PTT · Test ortamı" : "PTT · Canlı";
    public bool IsSimulation => false;

    // ---------------- Gönderi kaydı ----------------

    public async Task<CarrierShipmentResult> CreateShipmentAsync(CarrierShipmentRequest r, CancellationToken ct = default)
    {
        // dosyaAdi: PTT'de bu yükleme partisinin adı; kaydı silerken (barkodVeriSil) tekrar gerekir.
        var fileName = $"MEKTUP-{DateTime.UtcNow:yyyyMMddHHmmss}-{r.Reference}";
        var (senderFirst, senderLast) = SplitName(r.Sender.FullName);

        var item = new XElement(KabulXsd + "dongu",
            E("aAdres", r.Recipient.AddressLine),
            E("agirlik", r.WeightGrams),
            E("aliciAdi", r.Recipient.FullName),
            E("aliciEmail", r.Recipient.Email),
            E("aliciIlAdi", r.Recipient.City),
            E("aliciIlceAdi", r.Recipient.District),
            E("aliciSms", r.Recipient.Phone),
            E("aliciTel", r.Recipient.Phone),
            E("barkodNo", r.Barcode),
            E("boy", 0),
            E("deger_ucreti", 0),
            E("desi", 0),
            E("ekhizmet", _o.ExtraService),
            E("en", 0),
            new XElement(KabulXsd + "gondericibilgi",
                E("gonderici_adi", senderFirst),
                E("gonderici_adresi", $"{r.Sender.AddressLine} {r.Sender.District}/{r.Sender.City}"),
                E("gonderici_email", r.Sender.Email),
                E("gonderici_il_ad", r.Sender.City),
                E("gonderici_ilce_ad", r.Sender.District),
                E("gonderici_posta_kodu", r.Sender.PostalCode),
                E("gonderici_sms", r.Sender.Phone),
                E("gonderici_soyadi", senderLast),
                E("gonderici_telefonu", r.Sender.Phone),
                E("gonderici_ulke_id", "TR")),
            E("musteriReferansNo", r.Reference),
            E("odeme_sart_ucreti", 0),
            E("odemesekli", _o.PaymentMethod),
            E("yukseklik", 0));

        var body = new XElement(Kabul + "kabulEkle2",
            new XElement(Kabul + "input",
                item,
                E("dosyaAdi", fileName),
                E("gonderiTip", _o.ShipmentKind),
                E("gonderiTur", _o.ShipmentType),
                E("kullanici", _o.Username),
                E("musteriId", _o.CustomerId),
                E("sifre", _o.Password)));

        var response = await SendAsync(_o.UploadUrl, "urn:kabulEkle2", body, ct);
        if (response.Fault is { } fault)
            return new CarrierShipmentResult(false, null, null, fault);

        var ret = response.Return!;
        var errorCode = Int(ret, "hataKodu");
        var row = ret.Elements().FirstOrDefault(e => e.Name.LocalName == "dongu");
        var rowOk = row is null || Text(row, "donguSonuc")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        // hataKodu = 1 başarılı; satır bazında da donguSonuc kontrol edilir.
        if (errorCode != 1 || !rowOk)
        {
            var message = (row is null ? null : Text(row, "donguAciklama")) ?? Text(ret, "aciklama") ?? $"hataKodu={errorCode}";
            logger.LogWarning("PTT kabulEkle2 reddetti: {Barcode} {Code} {Message}", r.Barcode, errorCode, message);
            return new CarrierShipmentResult(false, null, null, message);
        }

        var barcode = (row is null ? null : Text(row, "barkod")) ?? r.Barcode;
        logger.LogInformation("PTT gönderi kaydı açıldı: {Barcode} ({File})", barcode, fileName);
        return new CarrierShipmentResult(true, barcode, fileName, null);
    }

    // ---------------- Gönderi takip ----------------

    public async Task<CarrierTrackingResult> TrackAsync(string barcode, CancellationToken ct = default)
    {
        var body = new XElement(Takip + "gonderiSorgu2",
            new XElement(Takip + "input",
                new XElement(TakipXsd + "barkod", barcode),
                new XElement(TakipXsd + "kullanici", _o.TrackingUsername ?? _o.Username),
                new XElement(TakipXsd + "sifre", _o.TrackingPassword ?? _o.Password)));

        var response = await SendAsync(_o.TrackingUrl, "urn:gonderiSorgu2", body, ct);
        if (response.Fault is { } fault)
            return CarrierTrackingResult.NotFound(fault);

        var ret = response.Return!;
        var movements = ret.Elements()
            .Where(e => e.Name.LocalName == "dongu")
            .Select(d => new
            {
                Order = Int(d, "siraNo") ?? 0,
                Movement = new CarrierMovement(ParseDate(Text(d, "ITARIH")), Text(d, "ISLEM") ?? "-", Text(d, "IMERK"))
            })
            .OrderBy(x => x.Order).ThenBy(x => x.Movement.At)
            .Select(x => x.Movement)
            .ToList();

        if (movements.Count == 0)
            return CarrierTrackingResult.NotFound(Text(ret, "sonucAciklama") ?? $"sonucKodu={Int(ret, "sonucKodu")}");

        // Teslim: "teslim alan" doluysa veya son işlem "TESLİM EDİLDİ" ise.
        var receivedBy = Text(ret, "TESALAN");
        var delivered = !string.IsNullOrWhiteSpace(receivedBy) || IsDeliveredText(movements[^1].Description);
        return new CarrierTrackingResult(true, delivered, receivedBy, movements, null);
    }

    // ---------------- SOAP altyapısı ----------------

    private sealed record SoapResponse(XElement? Return, string? Fault);

    private async Task<SoapResponse> SendAsync(string url, string action, XElement body, CancellationToken ct)
    {
        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", body)));

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.Add("SOAPAction", action);

        using var response = await http.SendAsync(request, ct);
        var xml = await response.Content.ReadAsStringAsync(ct);

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (System.Xml.XmlException)
        {
            // Şifre içerebilecek isteği değil, sadece yanıtın başını logla.
            logger.LogError("PTT {Action} geçersiz yanıt ({Status}): {Body}", action, (int)response.StatusCode, Truncate(xml));
            return new SoapResponse(null, $"PTT servisi geçersiz yanıt döndü (HTTP {(int)response.StatusCode}).");
        }

        var faultEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (faultEl is not null)
        {
            var reason = faultEl.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value;
            logger.LogWarning("PTT {Action} SOAP hatası: {Reason}", action, reason);
            return new SoapResponse(null, reason ?? "PTT servisi hata döndü.");
        }

        var ret = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "return");
        return ret is null
            ? new SoapResponse(null, $"PTT yanıtında sonuç bulunamadı (HTTP {(int)response.StatusCode}).")
            : new SoapResponse(ret, null);
    }

    /// <summary>Gönderi kaydı alanı (http://kabul.ptt.gov.tr/xsd). Boş değerler gönderilmez.</summary>
    private static XElement? E(string name, object? value) => value switch
    {
        null => null,
        string s when string.IsNullOrWhiteSpace(s) => null,
        IFormattable f => new XElement(KabulXsd + name, f.ToString(null, CultureInfo.InvariantCulture)),
        _ => new XElement(KabulXsd + name, value.ToString())
    };

    private static string? Text(XElement parent, string localName)
    {
        var v = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static int? Int(XElement parent, string localName) =>
        int.TryParse(Text(parent, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyyMMddHHmmss", "yyyyMMdd", "dd/MM/yyyy", "dd.MM.yyyy"
    ];

    /// <summary>PTT tarihleri Türkiye saatiyle gelir; UTC'ye çevrilir.</summary>
    internal static DateTime? ParseDate(string? value)
    {
        if (value is null) return null;
        if (!DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) &&
            !DateTime.TryParse(value, Tr, DateTimeStyles.None, out local))
            return null;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Turkey);
    }

    internal static bool IsDeliveredText(string text)
    {
        var t = text.ToUpper(Tr).Replace('İ', 'I');
        return t.Contains("TESLIM EDILDI") && !t.Contains("EDILEMEDI");
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? (fullName, "-") : (string.Join(' ', parts[..^1]), parts[^1]);
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
