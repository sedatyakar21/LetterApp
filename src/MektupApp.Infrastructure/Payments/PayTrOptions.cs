namespace MektupApp.Infrastructure.Payments;

/// <summary>
/// Ödeme ayarları (appsettings → "Payment"). PayTR mağaza bilgileri PayTR Mağaza Paneli →
/// Destek &amp; Kurulum → Entegrasyon Bilgileri'nde bulunur. Anahtarları appsettings.json'a yazmayın:
/// user-secrets veya ortam değişkeni kullanın (Payment__PayTR__MerchantKey, Payment__PayTR__MerchantSalt).
/// </summary>
public sealed class PaymentOptions
{
    public const string Section = "Payment";

    /// <summary>"Simulation" (varsayılan, gerçek tahsilat yok) veya "PayTR".</summary>
    public string Provider { get; set; } = "Simulation";

    /// <summary>Sonucu gelmeyen ödeme denemesi bu süreden sonra kapatılır (PayTR timeout_limit'ten uzun olmalı).</summary>
    public int ExpireAfterMinutes { get; set; } = 45;

    public PayTrOptions PayTR { get; set; } = new();

    public bool UsePayTr => Provider.Equals("PayTR", StringComparison.OrdinalIgnoreCase);
}

public sealed class PayTrOptions
{
    public string BaseUrl { get; set; } = "https://www.paytr.com";
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantKey { get; set; } = string.Empty;
    public string MerchantSalt { get; set; } = string.Empty;

    /// <summary>true: canlı mağazada test işlemi (test kartlarıyla). Canlıya geçince false yapın.</summary>
    public bool TestMode { get; set; } = true;

    /// <summary>true: PayTR hata mesajlarını döndürür (sadece entegrasyon sırasında).</summary>
    public bool DebugOn { get; set; } = true;

    /// <summary>1: taksit seçeneklerini gizle (tek çekim).</summary>
    public bool NoInstallment { get; set; } = true;

    /// <summary>0: izin verilen en fazla taksit; 2–12 arası sınır.</summary>
    public int MaxInstallment { get; set; }

    /// <summary>Ödeme formunun geçerlilik süresi (dakika).</summary>
    public int TimeoutLimitMinutes { get; set; } = 30;

    public string Currency { get; set; } = "TL";
    public string Lang { get; set; } = "tr";
    public int HttpTimeoutSeconds { get; set; } = 20;
}
