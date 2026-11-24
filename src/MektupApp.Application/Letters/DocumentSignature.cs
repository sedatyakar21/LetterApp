namespace MektupApp.Application.Letters;

/// <summary>
/// Yüklenen belgenin içeriğinin uzantısıyla uyuştuğunu dosya imzasından (ilk baytlar) kontrol eder.
/// Böylece ör. adı ".pdf" yapılmış bir çalıştırılabilir dosya veya HTML kabul edilmez.
/// </summary>
public static class DocumentSignature
{
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    public static bool Matches(ReadOnlySpan<byte> content, string extension) => extension.ToLowerInvariant() switch
    {
        // PDF başlığı dosyanın ilk 1 KB'ı içinde olabilir (spesifikasyon buna izin verir).
        ".pdf" => content[..Math.Min(content.Length, 1024)].IndexOf(Pdf) >= 0,
        ".png" => content.StartsWith(Png),
        ".jpg" or ".jpeg" => content.StartsWith(Jpeg),
        _ => false
    };
}
