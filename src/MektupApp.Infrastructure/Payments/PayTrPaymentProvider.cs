using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MektupApp.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Payments;

/// <summary>
/// PayTR iFrame API adapter'ı (dev.paytr.com):
///  - Adım 1: /odeme/api/get-token → token alınır, müşteri /odeme/guvenli/{token} iFrame'inde öder.
///  - Adım 2: PayTR, mağaza panelinde tanımlı "Bildirim URL"ye sonucu POST eder; hash doğrulanır.
///  - İade:   /odeme/iade
/// </summary>
public sealed class PayTrPaymentProvider(HttpClient http, IOptions<PaymentOptions> options, ILogger<PayTrPaymentProvider> logger)
    : IPaymentProvider
{
    private readonly PayTrOptions _o = options.Value.PayTR;

    public string Name => "PayTR";
    public string EnvironmentLabel => _o.TestMode ? "PayTR · Test modu" : "PayTR · Canlı";
    public bool IsSimulation => false;
    public bool SupportsMobilePayment => false;

    // ---------------- Adım 1: token ----------------

    public async Task<PaymentStartResult> StartAsync(PaymentStartRequest r, CancellationToken ct = default)
    {
        var amountKurus = ToKurus(r.Amount).ToString(CultureInfo.InvariantCulture);
        var basket = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            r.Basket.Select(b => new object[] { b.Name, b.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture), b.Quantity }))));
        var noInstallment = _o.NoInstallment ? "1" : "0";
        var maxInstallment = _o.MaxInstallment.ToString(CultureInfo.InvariantCulture);
        var testMode = _o.TestMode ? "1" : "0";
        var email = AsciiEmail(r.Buyer.Email);
        var userIp = NormalizeIp(r.Buyer.IpAddress);

        // Sıra dokümandaki gibi olmalı; sonuna merchant_salt eklenir.
        var token = Sign(_o.MerchantId + userIp + r.MerchantOid + email + amountKurus + basket +
                         noInstallment + maxInstallment + _o.Currency + testMode + _o.MerchantSalt);

        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = _o.MerchantId,
            ["user_ip"] = userIp,
            ["merchant_oid"] = r.MerchantOid,
            ["email"] = email,
            ["payment_amount"] = amountKurus,
            ["paytr_token"] = token,
            ["user_basket"] = basket,
            ["debug_on"] = _o.DebugOn ? "1" : "0",
            ["no_installment"] = noInstallment,
            ["max_installment"] = maxInstallment,
            ["user_name"] = Limit(r.Buyer.FullName, 60),
            ["user_address"] = Limit(r.Buyer.Address, 400),
            ["user_phone"] = Limit(r.Buyer.Phone, 20),
            ["merchant_ok_url"] = r.SuccessUrl,
            ["merchant_fail_url"] = r.FailUrl,
            ["timeout_limit"] = _o.TimeoutLimitMinutes.ToString(CultureInfo.InvariantCulture),
            ["currency"] = _o.Currency,
            ["test_mode"] = testMode,
            ["lang"] = _o.Lang
        };

        using var response = await http.PostAsync($"{_o.BaseUrl.TrimEnd('/')}/odeme/api/get-token", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        try
        {
            using var json = JsonDocument.Parse(body);
            var status = json.RootElement.GetProperty("status").GetString();
            if (status == "success" && json.RootElement.TryGetProperty("token", out var t) && t.GetString() is { Length: > 0 } iframeToken)
                return new PaymentStartResult(true, $"{_o.BaseUrl.TrimEnd('/')}/odeme/guvenli/{iframeToken}", null);

            var reason = json.RootElement.TryGetProperty("reason", out var rs) ? rs.GetString() : null;
            logger.LogWarning("PayTR get-token reddetti: {Oid} {Reason}", r.MerchantOid, reason);
            return new PaymentStartResult(false, null, reason ?? "PayTR ödeme formu oluşturulamadı.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError("PayTR get-token geçersiz yanıt ({Status}): {Body}", (int)response.StatusCode, Truncate(body));
            return new PaymentStartResult(false, null, $"PayTR geçersiz yanıt döndü (HTTP {(int)response.StatusCode}).");
        }
    }

    // ---------------- Adım 2: bildirim ----------------

    public PaymentNotification? VerifyNotification(IReadOnlyDictionary<string, string> form)
    {
        string? Get(string key) => form.TryGetValue(key, out var v) ? v : null;

        var oid = Get("merchant_oid");
        var status = Get("status");
        var total = Get("total_amount");
        var hash = Get("hash");
        if (oid is null || status is null || total is null || hash is null) return null;

        // hash = base64(HMAC-SHA256(merchant_key, merchant_oid + merchant_salt + status + total_amount))
        var expected = Sign(oid + _o.MerchantSalt + status + total);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(hash)))
        {
            logger.LogWarning("PayTR bildirimi hash doğrulamasından geçmedi: {Oid}", oid);
            return null;
        }

        if (!long.TryParse(total, NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalKurus)) return null;
        var success = status == "success";
        var reason = success ? null : $"{Get("failed_reason_msg") ?? "Ödeme onaylanmadı"} (kod {Get("failed_reason_code") ?? "-"})";

        return new PaymentNotification(oid, success, totalKurus / 100m, reason, Get("test_mode") == "1");
    }

    // ---------------- İade ----------------

    public async Task<PaymentRefundResult> RefundAsync(string merchantOid, decimal amount, CancellationToken ct = default)
    {
        var returnAmount = amount.ToString("0.00", CultureInfo.InvariantCulture); // ondalık ayırıcı nokta
        var form = new Dictionary<string, string>
        {
            ["merchant_id"] = _o.MerchantId,
            ["merchant_oid"] = merchantOid,
            ["return_amount"] = returnAmount,
            ["paytr_token"] = Sign(_o.MerchantId + merchantOid + returnAmount + _o.MerchantSalt)
        };

        try
        {
            using var response = await http.PostAsync($"{_o.BaseUrl.TrimEnd('/')}/odeme/iade", new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.GetProperty("status").GetString() == "success")
            {
                logger.LogInformation("PayTR iade başarılı: {Oid} {Amount}", merchantOid, returnAmount);
                return new PaymentRefundResult(true, null);
            }
            var error = json.RootElement.TryGetProperty("err_msg", out var e) ? e.GetString() : body;
            logger.LogWarning("PayTR iade reddedildi: {Oid} {Error}", merchantOid, error);
            return new PaymentRefundResult(false, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException)
        {
            logger.LogError(ex, "PayTR iade isteği başarısız: {Oid}", merchantOid);
            return new PaymentRefundResult(false, "PayTR'ye ulaşılamadı: " + ex.Message);
        }
    }

    // ---------------- yardımcılar ----------------

    internal string Sign(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_o.MerchantKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    /// <summary>34,56 ₺ → 3456 (PayTR tutarları kuruş cinsinden ister).</summary>
    internal static long ToKurus(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>PayTR e-postada Türkçe karakter kabul etmez.</summary>
    private static string AsciiEmail(string email) => Limit(email
        .Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S').Replace('ğ', 'g').Replace('Ğ', 'G')
        .Replace('ü', 'u').Replace('Ü', 'U').Replace('ö', 'o').Replace('Ö', 'O').Replace('ç', 'c').Replace('Ç', 'C'), 100);

    /// <summary>IPv4'e eşlenmiş IPv6 adreslerini sadeleştirir (::ffff:1.2.3.4 → 1.2.3.4).</summary>
    private static string NormalizeIp(string ip) =>
        Limit(ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase) ? ip[7..] : ip, 39);

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
