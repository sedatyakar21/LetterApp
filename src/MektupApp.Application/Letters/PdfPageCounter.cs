using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MektupApp.Application.Letters;

/// <summary>
/// PDF'in sayfa sayısını (ağırlık hesabı için yaprak sayısı) dış kütüphane olmadan tahmin eder.
/// 1) Sayfa nesnelerini ("/Type /Page") sayar; 2) yeni PDF'lerde bu nesneler sıkıştırılmış nesne akışlarında
/// (/ObjStm) durduğundan o akışları açıp orada da sayar; 3) bulamazsa sayfa ağacındaki en büyük "/Count" değerini alır.
/// Hiçbiri bulunamazsa 1 döner. Sonuç en fazla <see cref="MaxPages"/> ile sınırlıdır.
/// </summary>
public static partial class PdfPageCounter
{
    public const int MaxPages = 500;
    private const int MaxInflatedBytes = 8 * 1024 * 1024;

    [GeneratedRegex(@"/Type\s*/Page(?![A-Za-z])")]
    private static partial Regex PageObject();

    [GeneratedRegex(@"/Type\s*/Pages\b[^>]*?/Count\s+(\d+)|/Count\s+(\d+)[^>]*?/Type\s*/Pages\b", RegexOptions.Singleline)]
    private static partial Regex PagesCount();

    [GeneratedRegex(@"<<((?:(?!>>).){0,400}?)>>\s*stream\r?\n", RegexOptions.Singleline)]
    private static partial Regex StreamStart();

    public static int Count(ReadOnlySpan<byte> pdf)
    {
        try
        {
            // Latin1: her bayt tek karakter; ikili içerik bozulmadan metin olarak taranır.
            var text = Encoding.Latin1.GetString(pdf);
            var pages = PageObject().Count(text);
            var maxCount = MaxPagesCount(text);

            if (pages == 0 || maxCount == 0)
            {
                foreach (var inner in ObjectStreams(text))
                {
                    pages += PageObject().Count(inner);
                    maxCount = Math.Max(maxCount, MaxPagesCount(inner));
                }
            }

            var result = pages > 0 ? pages : maxCount;
            return Math.Clamp(result, 1, MaxPages);
        }
        catch
        {
            return 1; // bozuk / beklenmeyen biçim: ağırlık hesabı yine çalışsın
        }
    }

    private static int MaxPagesCount(string text)
    {
        var max = 0;
        foreach (Match m in PagesCount().Matches(text))
        {
            var g = m.Groups[1].Success ? m.Groups[1] : m.Groups[2];
            if (int.TryParse(g.Value, out var n)) max = Math.Max(max, n);
        }
        return max;
    }

    /// <summary>FlateDecode ile sıkıştırılmış nesne akışlarının (/ObjStm) açılmış içerikleri.</summary>
    private static IEnumerable<string> ObjectStreams(string text)
    {
        foreach (Match m in StreamStart().Matches(text))
        {
            var dict = m.Groups[1].Value;
            if (!dict.Contains("/ObjStm") || !dict.Contains("/FlateDecode")) continue;

            var start = m.Index + m.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end <= start) continue;

            string? inflated = null;
            try
            {
                var bytes = Encoding.Latin1.GetBytes(text[start..end]);
                using var input = new MemoryStream(bytes);
                using var z = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                // Sıkıştırma bombasına karşı: akış başına en fazla 8 MB açılır.
                var buffer = new byte[81920];
                int read;
                while ((read = z.Read(buffer, 0, buffer.Length)) > 0 && output.Length < MaxInflatedBytes)
                    output.Write(buffer, 0, read);
                if (output.Length > 0) inflated = Encoding.Latin1.GetString(output.GetBuffer(), 0, (int)output.Length);
            }
            catch (InvalidDataException) { /* bozuk akış: atla */ }

            if (inflated is not null) yield return inflated;
        }
    }
}
