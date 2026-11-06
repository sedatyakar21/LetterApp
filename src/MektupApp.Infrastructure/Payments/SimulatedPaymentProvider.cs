using MektupApp.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace MektupApp.Infrastructure.Payments;

/// <summary>
/// Geliştirme için sahte ödeme sağlayıcısı (Payment:Provider = "Simulation"). Gerçek tahsilat yapmaz.
/// Ödeme formu olarak uygulamanın kendi simülasyon sayfasını (/odeme/simulasyon/{oid}) verir;
/// orada "Başarılı" / "Başarısız" seçilerek PayTR bildirimi taklit edilir.
/// Dışarıdan gelen bildirimleri hiçbir zaman kabul etmez.
/// </summary>
public sealed class SimulatedPaymentProvider(ILogger<SimulatedPaymentProvider> logger) : IPaymentProvider
{
    public const string FormPathPrefix = "/odeme/simulasyon/";

    public string Name => "Simülasyon";
    public string EnvironmentLabel => "Ödeme · Simülasyon (gerçek tahsilat yok)";
    public bool IsSimulation => true;
    public bool SupportsMobilePayment => true;

    public Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("[Simülasyon] Ödeme formu: {Oid} {Amount} ₺", request.MerchantOid, request.Amount);
        return Task.FromResult(new PaymentStartResult(true, FormPathPrefix + request.MerchantOid, null));
    }

    public PaymentNotification? VerifyNotification(IReadOnlyDictionary<string, string> form) => null;

    public Task<PaymentRefundResult> RefundAsync(string merchantOid, decimal amount, CancellationToken ct = default)
    {
        logger.LogInformation("[Simülasyon] İade: {Oid} {Amount} ₺", merchantOid, amount);
        return Task.FromResult(new PaymentRefundResult(true, null));
    }
}
