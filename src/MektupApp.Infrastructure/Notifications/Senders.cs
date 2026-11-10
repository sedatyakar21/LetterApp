using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using MailKit.Net.Smtp;
using MailKit.Security;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MektupApp.Infrastructure.Notifications;

/// <summary>Notifications:Sms:Netgsm. Şifre user-secrets / ortam değişkeninden verilir.</summary>
public sealed class NetgsmOptions
{
    public const string Section = "Notifications:Sms:Netgsm";
    public string? UserCode { get; set; }
    public string? Password { get; set; }
    /// <summary>Netgsm'de onaylı gönderici adı (mesaj başlığı), ör. "BIMEKTUP".</summary>
    public string? Header { get; set; }
    public string Endpoint { get; set; } = "https://api.netgsm.com.tr/sms/send/xml";
    public int TimeoutSeconds { get; set; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserCode) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(Header);
}

/// <summary>Notifications:Email:Smtp. Şifre user-secrets / ortam değişkeninden verilir.</summary>
public sealed class SmtpOptions
{
    public const string Section = "Notifications:Email:Smtp";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    /// <summary>"StartTls" (587), "SslOnConnect" (465) veya "None" (yalnızca yerel test sunucusu).</summary>
    public string Security { get; set; } = "StartTls";
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "BiMektup";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

/// <summary>Sağlayıcı tanımlı değilken SMS'i yalnızca log'a yazar (geliştirme / simülasyon).</summary>
internal sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public bool IsSimulation => true;
    public string ProviderName => "Simülasyon (log)";

    public Task<SendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        logger.LogInformation("SMS (simülasyon) -> {Phone}: {Message}", phoneNumber, message);
        return Task.FromResult(SendResult.Ok("sim-" + Guid.NewGuid().ToString("N")[..12]));
    }
}

/// <summary>Sağlayıcı tanımlı değilken e-postayı yalnızca log'a yazar (geliştirme / simülasyon).</summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public bool IsSimulation => true;
    public string ProviderName => "Simülasyon (log)";

    public Task<SendResult> SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("E-posta (simülasyon) -> {To}: {Subject}", to, subject);
        return Task.FromResult(SendResult.Ok("sim-" + Guid.NewGuid().ToString("N")[..12]));
    }
}

/// <summary>
/// Netgsm XML POST API ile SMS (bilgilendirme amaçlı; İYS filtresi 0). Türkçe karakter için dil="TR".
/// Kimlik bilgileri adreste değil istek gövdesinde gider. Yanıt "00 &lt;bulkid&gt;" ise başarılıdır.
/// </summary>
internal sealed class NetgsmSmsSender(HttpClient http, IOptions<NetgsmOptions> options, ILogger<NetgsmSmsSender> logger) : ISmsSender
{
    public bool IsSimulation => false;
    public string ProviderName => "Netgsm";

    // Netgsm hata kodları: tekrar denemenin anlamı olmayanlar kalıcı sayılır.
    private static readonly Dictionary<string, (string Message, bool Permanent)> Errors = new()
    {
        ["20"] = ("Mesaj metni hatalı veya çok uzun.", true),
        ["30"] = ("Netgsm kullanıcı adı / şifre hatalı, API yetkisi yok veya IP kısıtı.", true),
        ["40"] = ("Gönderici adı (mesaj başlığı) Netgsm hesabında tanımlı değil.", true),
        ["50"] = ("Abone hesabıyla İYS kontrollü gönderim yapılamaz.", true),
        ["51"] = ("İYS marka bilgisi bulunamadı.", true),
        ["70"] = ("Hatalı sorgu: parametreler eksik veya hatalı.", true),
        ["80"] = ("Gönderim sınırı aşıldı.", false),
        ["85"] = ("Aynı numaraya kısa sürede çok fazla mesaj.", false)
    };

    public async Task<SendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var o = options.Value;
        var xml = BuildRequest(o, phoneNumber, message);

        using var request = new HttpRequestMessage(HttpMethod.Post, o.Endpoint)
        {
            Content = new StringContent(xml, Encoding.UTF8, new MediaTypeHeaderValue("application/xml"))
        };
        using var response = await http.SendAsync(request, ct);
        var body = (await response.Content.ReadAsStringAsync(ct)).Trim();
        if (!response.IsSuccessStatusCode)
            return SendResult.Fail($"Netgsm HTTP {(int)response.StatusCode}");

        return ParseResponse(body, logger);
    }

    internal static string BuildRequest(NetgsmOptions o, string phoneNumber, string message) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        new XElement("mainbody",
            new XElement("header",
                new XElement("company", new XAttribute("dil", "TR"), "Netgsm"),
                new XElement("usercode", o.UserCode),
                new XElement("password", o.Password),
                new XElement("type", "1:n"),
                new XElement("msgheader", o.Header),
                new XElement("filter", "0")),                 // bilgilendirme mesajı (ticari değil, İYS'ye tabi değil)
            new XElement("body",
                new XElement("msg", new XCData(message)),
                new XElement("no", phoneNumber)))
        .ToString(SaveOptions.DisableFormatting);

    internal static SendResult ParseResponse(string body, ILogger? logger = null)
    {
        // Başarılı: "00 1234567890" (bazı hesaplarda "01"/"02" tarih düzeltmesiyle kabul edilir).
        var parts = body.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var code = parts.FirstOrDefault() ?? "";
        if (code is "00" or "01" or "02")
            return SendResult.Ok(parts.Length > 1 ? parts[1] : null);

        logger?.LogWarning("Netgsm SMS reddedildi: {Body}", body);
        return Errors.TryGetValue(code, out var e)
            ? SendResult.Fail($"Netgsm {code}: {e.Message}", e.Permanent)
            : SendResult.Fail($"Netgsm beklenmeyen yanıt: {(body.Length > 80 ? body[..80] : body)}");
    }
}

/// <summary>SMTP ile e-posta (MailKit). Her gönderimde bağlanıp kapatır; düşük hacim için yeterlidir.</summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public bool IsSimulation => false;
    public string ProviderName => $"SMTP ({options.Value.Host})";

    public async Task<SendResult> SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        try { message.To.Add(MailboxAddress.Parse(to)); }
        catch (ParseException) { return SendResult.Fail("Geçersiz e-posta adresi.", permanent: true); }
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 20_000 };
        try
        {
            var security = o.Security switch
            {
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                "None" => SecureSocketOptions.None,
                _ => SecureSocketOptions.StartTls
            };
            await client.ConnectAsync(o.Host, o.Port, security, ct);
            if (!string.IsNullOrWhiteSpace(o.UserName))
                await client.AuthenticateAsync(o.UserName, o.Password ?? "", ct);
            var response = await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            return SendResult.Ok(message.MessageId ?? response);
        }
        catch (AuthenticationException ex) { return SendResult.Fail("SMTP kimlik doğrulama hatası: " + ex.Message, permanent: true); }
        catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted)
        {
            return SendResult.Fail("Alıcı adresi reddedildi: " + ex.Message, permanent: true);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or IOException or System.Net.Sockets.SocketException)
        {
            return SendResult.Fail("SMTP hatası: " + ex.Message);
        }
    }
}

/// <summary>Kuyruktaki bildirimleri birkaç saniyede bir gönderir.</summary>
public sealed class NotificationSenderService(IServiceScopeFactory scopes, ILogger<NotificationSenderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().SendDueAsync(50, stoppingToken);
                if (sent > 0) logger.LogInformation("{Count} bildirim işlendi.", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bildirim kuyruğu işlenemedi.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
