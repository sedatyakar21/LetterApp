namespace MektupApp.Application.Abstractions;

// Ödeme sağlayıcısı portu. Ödeme asenkrondur: müşteri sağlayıcının formunda (PayTR iFrame) öder,
// kesin sonuç sağlayıcının sunucumuza gönderdiği bildirimle gelir. Müşterinin geri yönlendirildiği
// "başarılı / başarısız" sayfaları sonuç kabul edilmez.
// Uygulamaları Infrastructure'da: PayTrPaymentProvider ve SimulatedPaymentProvider.

public sealed record PaymentBuyer(string FullName, string Email, string Phone, string Address, string IpAddress);

public sealed record PaymentBasketItem(string Name, decimal UnitPrice, int Quantity);

/// <param name="SuccessUrl">Müşterinin ödeme sonrası döneceği adres (sadece bilgilendirme sayfası).</param>
/// <param name="FailUrl">Ödeme formunda beklenmeyen hata olursa döneceği adres.</param>
public sealed record PaymentStartRequest(
    string MerchantOid,
    decimal Amount,
    PaymentBuyer Buyer,
    IReadOnlyList<PaymentBasketItem> Basket,
    string SuccessUrl,
    string FailUrl);

/// <param name="FormUrl">Müşterinin ödeme yapacağı form (PayTR iFrame adresi).</param>
public sealed record PaymentStartResult(bool Succeeded, string? FormUrl, string? Error);

/// <summary>Sağlayıcıdan gelen, imzası doğrulanmış ödeme sonucu.</summary>
public sealed record PaymentNotification(
    string MerchantOid,
    bool Success,
    decimal TotalAmount,
    string? FailureReason,
    bool IsTest);

public sealed record PaymentRefundResult(bool Succeeded, string? Error);

public interface IPaymentProvider
{
    /// <summary>Kısa ad (ör. "PayTR"); ödeme kayıtlarında görünür.</summary>
    string Name { get; }

    /// <summary>Ekranda gösterilecek ortam bilgisi (ör. "PayTR · Test modu").</summary>
    string EnvironmentLabel { get; }

    bool IsSimulation { get; }

    /// <summary>Sağlayıcı operatör (mobil) ödemesini destekliyor mu? PayTR iFrame kart / EFT içindir.</summary>
    bool SupportsMobilePayment { get; }

    /// <summary>Ödeme formunu hazırlar (PayTR: get-token).</summary>
    Task<PaymentStartResult> StartAsync(PaymentStartRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sağlayıcının bildirim isteğini doğrular ve çözümler. İmza geçersizse null döner
    /// (istek sağlayıcıdan gelmiyor demektir; işlem yapılmamalı).
    /// </summary>
    PaymentNotification? VerifyNotification(IReadOnlyDictionary<string, string> form);

    Task<PaymentRefundResult> RefundAsync(string merchantOid, decimal amount, CancellationToken ct = default);
}
