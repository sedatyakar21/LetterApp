namespace MektupApp.Infrastructure.Carriers;

/// <summary>
/// PTT web servis ayarları (appsettings → "Ptt"). Müşteri no, şifre ve barkod aralığı PTT ile
/// yapılan sözleşme sonrası PTT tarafından verilir (iletişim: entegrasyon@ptt.gov.tr).
/// Şifreleri appsettings.json'a yazmayın: user-secrets veya ortam değişkeni kullanın
/// (ör. Ptt__Password, Ptt__TrackingPassword).
/// </summary>
public sealed class PttOptions
{
    public const string Section = "Ptt";

    /// <summary>false iken gerçek PTT yerine simülasyon taşıyıcısı kullanılır.</summary>
    public bool Enabled { get; set; }

    /// <summary>true: PttVeriYuklemeTest / GonderiTakipV2Test servisleri. PTT onayından sonra false yapılır.</summary>
    public bool UseTestEnvironment { get; set; } = true;

    public string BaseUrl { get; set; } = "https://pttws.ptt.gov.tr";

    // Gönderi kaydı (kabulEkle2) kimlik bilgileri
    public int CustomerId { get; set; }
    public string Username { get; set; } = "PttWs";
    public string Password { get; set; } = string.Empty;

    // Gönderi takip (gonderiSorgu2) kimlik bilgileri; boşsa gönderi kaydı bilgileri kullanılır
    public string? TrackingUsername { get; set; }
    public string? TrackingPassword { get; set; }

    // Gönderi türü kodları: PTT sözleşmenizdeki değerlerle doğrulayın.
    public string ShipmentType { get; set; } = "KARGO";      // gonderiTur
    public string ShipmentKind { get; set; } = "NORMAL";     // gonderiTip
    public string PaymentMethod { get; set; } = "MH";        // odemesekli (MH: mahsup / sözleşmeli)
    public string? ExtraService { get; set; }                 // ekhizmet

    public int TimeoutSeconds { get; set; } = 30;

    public BarcodeOptions Barcode { get; set; } = new();
    public SyncOptions Sync { get; set; } = new();
    public SimulationOptions Simulation { get; set; } = new();

    public string UploadUrl => $"{BaseUrl.TrimEnd('/')}/{(UseTestEnvironment ? "PttVeriYuklemeTest" : "PttVeriYukleme")}/services/Sorgu";
    public string TrackingUrl => $"{BaseUrl.TrimEnd('/')}/{(UseTestEnvironment ? "GonderiTakipV2Test" : "GonderiTakipV2")}/services/Sorgu";
}

/// <summary>PTT'nin tahsis ettiği barkod aralığı.</summary>
public sealed class BarcodeOptions
{
    /// <summary>"S10": önek + 8 hane + kontrol hanesi + sonek (RR123456785TR). "Cargo": önek + 11 hane (KP12345678901).</summary>
    public string Format { get; set; } = "S10";
    public string Prefix { get; set; } = "RR";
    public string Suffix { get; set; } = "TR";
    public long RangeStart { get; set; } = 10_000_000;
    public long RangeEnd { get; set; } = 19_999_999;
}

public sealed class SyncOptions
{
    /// <summary>Kargodaki mektupların hareketlerini arka planda otomatik çek.</summary>
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 30;
    /// <summary>Aynı mektubu en erken kaç dakikada bir tekrar sorgula.</summary>
    public int MinRecheckMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
}

public sealed class SimulationOptions
{
    /// <summary>Simülasyonda her hareket adımı arası süre (kabul → transfer → dağıtım → teslim).</summary>
    public int StepSeconds { get; set; } = 60;
}
