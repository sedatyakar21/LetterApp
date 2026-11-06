using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MektupApp.Application.Abstractions;
using MektupApp.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace MektupApp.Infrastructure.Tests;

/// <summary>
/// PayTR adapter'ı gerçek servise gitmeden test edilir. Token / hash formülleri testte,
/// dev.paytr.com'daki sıraya göre bağımsız olarak yeniden hesaplanır.
/// </summary>
public class PayTrPaymentProviderTests
{
    private const string MerchantId = "123456";
    private const string MerchantKey = "test-merchant-key";
    private const string MerchantSalt = "test-merchant-salt";

    private static PaymentOptions Options() => new()
    {
        Provider = "PayTR",
        PayTR = new PayTrOptions { MerchantId = MerchantId, MerchantKey = MerchantKey, MerchantSalt = MerchantSalt, TestMode = true }
    };

    private static PayTrPaymentProvider Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(Options()), NullLogger<PayTrPaymentProvider>.Instance);

    /// <summary>base64(HMAC-SHA256(merchant_key, data)) — PayTR dokümanındaki imza.</summary>
    private static string Hmac(string data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(MerchantKey));
        return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    private static PaymentStartRequest SampleStart() => new(
        "MK20261106120000ABCDEF12",
        290.50m,
        new PaymentBuyer("Ayşe Yılmaz", "ayse.yılmaz@example.com", "05551234567", "Atatürk Cad. 1, Nilüfer / Bursa 16110", "85.100.10.20"),
        [new PaymentBasketItem("Mektup gönderimi - Ali Veli", 290.50m, 1)],
        "https://mektupapp.example/mektup/1/odeme/sonuc?oid=X",
        "https://mektupapp.example/mektup/1/odeme/sonuc?oid=X");

    [Fact]
    public async Task Get_token_request_matches_paytr_spec()
    {
        var handler = new FormCapturingHandler("""{"status":"success","token":"abc123TOKEN"}""");
        var result = await Create(handler).StartAsync(SampleStart());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("https://www.paytr.com/odeme/guvenli/abc123TOKEN", result.FormUrl);
        Assert.Equal("https://www.paytr.com/odeme/api/get-token", handler.Url);

        var f = handler.Form!;
        Assert.Equal(MerchantId, f["merchant_id"]);
        Assert.Equal("29050", f["payment_amount"]);                 // kuruş
        Assert.Equal("ayse.yilmaz@example.com", f["email"]);        // Türkçe karakter temizlendi
        Assert.Equal("85.100.10.20", f["user_ip"]);
        Assert.Equal("1", f["test_mode"]);
        Assert.Equal("TL", f["currency"]);

        // Sepet: base64(JSON [[ad, birim fiyat, adet]])
        var basket = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(f["user_basket"]))).RootElement;
        Assert.Equal("Mektup gönderimi - Ali Veli", basket[0][0].GetString());
        Assert.Equal("290.50", basket[0][1].GetString());
        Assert.Equal(1, basket[0][2].GetInt32());

        // paytr_token: merchant_id + user_ip + merchant_oid + email + payment_amount + user_basket
        //              + no_installment + max_installment + currency + test_mode + merchant_salt
        var expected = Hmac(f["merchant_id"] + f["user_ip"] + f["merchant_oid"] + f["email"] + f["payment_amount"] + f["user_basket"] +
                            f["no_installment"] + f["max_installment"] + f["currency"] + f["test_mode"] + MerchantSalt);
        Assert.Equal(expected, f["paytr_token"]);
    }

    [Fact]
    public async Task Get_token_failure_returns_reason()
    {
        var handler = new FormCapturingHandler("""{"status":"failed","reason":"paytr_token gecersiz"}""");
        var result = await Create(handler).StartAsync(SampleStart());

        Assert.False(result.Succeeded);
        Assert.Equal("paytr_token gecersiz", result.Error);
    }

    [Fact]
    public void Valid_notification_is_accepted()
    {
        var form = new Dictionary<string, string>
        {
            ["merchant_oid"] = "MK20261106120000ABCDEF12",
            ["status"] = "success",
            ["total_amount"] = "30551",
            ["payment_amount"] = "29050",
            ["test_mode"] = "1",
            ["payment_type"] = "card",
            ["currency"] = "TL"
        };
        // hash = base64(HMAC(merchant_oid + merchant_salt + status + total_amount))
        form["hash"] = Hmac(form["merchant_oid"] + MerchantSalt + form["status"] + form["total_amount"]);

        var n = Create(new FormCapturingHandler("{}")).VerifyNotification(form);

        Assert.NotNull(n);
        Assert.True(n.Success);
        Assert.Equal(305.51m, n.TotalAmount);
        Assert.True(n.IsTest);
    }

    [Fact]
    public void Tampered_notification_is_rejected()
    {
        var form = new Dictionary<string, string>
        {
            ["merchant_oid"] = "MK20261106120000ABCDEF12",
            ["status"] = "success",
            ["total_amount"] = "100"
        };
        form["hash"] = Hmac(form["merchant_oid"] + MerchantSalt + "failed" + form["total_amount"]); // farklı durumla imzalanmış

        Assert.Null(Create(new FormCapturingHandler("{}")).VerifyNotification(form));
        Assert.Null(Create(new FormCapturingHandler("{}")).VerifyNotification(new Dictionary<string, string> { ["merchant_oid"] = "X" }));
    }

    [Fact]
    public void Failed_notification_carries_reason()
    {
        var form = new Dictionary<string, string>
        {
            ["merchant_oid"] = "MK1",
            ["status"] = "failed",
            ["total_amount"] = "29050",
            ["failed_reason_code"] = "2",
            ["failed_reason_msg"] = "Yetersiz bakiye"
        };
        form["hash"] = Hmac("MK1" + MerchantSalt + "failed" + "29050");

        var n = Create(new FormCapturingHandler("{}")).VerifyNotification(form)!;
        Assert.False(n.Success);
        Assert.Equal("Yetersiz bakiye (kod 2)", n.FailureReason);
    }

    [Fact]
    public async Task Refund_request_matches_paytr_spec()
    {
        var handler = new FormCapturingHandler("""{"status":"success","is_test":1,"merchant_oid":"MK1","return_amount":"290.5"}""");
        var result = await Create(handler).RefundAsync("MK1", 290.50m);

        Assert.True(result.Succeeded);
        Assert.Equal("https://www.paytr.com/odeme/iade", handler.Url);
        var f = handler.Form!;
        Assert.Equal("290.50", f["return_amount"]); // ondalık ayırıcı nokta
        Assert.Equal(Hmac(MerchantId + "MK1" + "290.50" + MerchantSalt), f["paytr_token"]);
    }

    [Fact]
    public async Task Refund_error_is_reported()
    {
        var handler = new FormCapturingHandler("""{"status":"error","err_no":"006","err_msg":"Iade tutari odeme tutarindan fazla olamaz"}""");
        var result = await Create(handler).RefundAsync("MK1", 999m);

        Assert.False(result.Succeeded);
        Assert.Equal("Iade tutari odeme tutarindan fazla olamaz", result.Error);
    }

    [Theory]
    [InlineData(34.56, 3456)]
    [InlineData(290, 29000)]
    [InlineData(0.005, 1)]
    public void Amount_is_sent_in_kurus(decimal amount, long kurus) =>
        Assert.Equal(kurus, PayTrPaymentProvider.ToKurus(amount));

    private sealed class FormCapturingHandler(string responseJson) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public Dictionary<string, string>? Form { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            var body = await request.Content!.ReadAsStringAsync(ct);
            Form = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
        }
    }
}
