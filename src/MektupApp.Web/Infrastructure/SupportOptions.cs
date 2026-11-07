namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Müşteri destek ayarları (appsettings → "Support"). WhatsApp numarası boşsa yardım butonu gösterilmez.
/// </summary>
public sealed class SupportOptions
{
    public const string Section = "Support";

    /// <summary>Ülke koduyla, sadece rakam (ör. 905321234567). wa.me bağlantısında kullanılır.</summary>
    public string? WhatsAppNumber { get; set; }

    /// <summary>Ekranda gösterilecek biçim (ör. "0532 123 45 67").</summary>
    public string? WhatsAppDisplay { get; set; }

    /// <summary>Çalışma saatleri bilgisi (ör. "Hafta içi 09:00–18:00").</summary>
    public string? Hours { get; set; }

    public string? Email { get; set; }

    public bool WhatsAppEnabled => !string.IsNullOrWhiteSpace(Digits);

    /// <summary>Numaradaki boşluk, +, tire vb. temizlenir.</summary>
    public string Digits => new((WhatsAppNumber ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>https://wa.me/{numara}?text={mesaj}</summary>
    public string WhatsAppLink(string message) =>
        $"https://wa.me/{Digits}?text={Uri.EscapeDataString(message)}";
}
