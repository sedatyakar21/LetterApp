using System.Globalization;
using System.Text;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Code 128 (B alt kümesi) barkodunu SVG olarak üretir. Zarf çıktısında takip numarasını okutulabilir
/// barkod olarak basmak için kullanılır; dış kütüphane gerektirmez.
/// </summary>
public static class Code128
{
    // Her değerin çubuk/boşluk genişlikleri (modül cinsinden; çubukla başlar). 103-105 başlangıç, 106 bitiş.
    public static readonly IReadOnlyList<string> Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    private const int StartB = 104;
    private const int Stop = 106;
    private const int QuietZone = 10; // modül

    /// <summary>Başlangıç (B) + veri + kontrol karakteri + bitiş değerleri.</summary>
    public static IReadOnlyList<int> Encode(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("Barkod metni boş olamaz.", nameof(text));
        var codes = new List<int> { StartB };
        foreach (var ch in text)
        {
            if (ch is < ' ' or > '~') throw new ArgumentException($"Code 128B bu karakteri desteklemez: '{ch}'.", nameof(text));
            codes.Add(ch - 32);
        }
        var checksum = StartB;
        for (var i = 1; i < codes.Count; i++) checksum += codes[i] * i;
        codes.Add(checksum % 103);
        codes.Add(Stop);
        return codes;
    }

    /// <summary>Toplam modül sayısı (sessiz bölgeler dahil).</summary>
    public static int ModuleCount(string text) =>
        Encode(text).Sum(c => Patterns[c].Sum(w => w - '0')) + 2 * QuietZone;

    /// <summary>
    /// Barkodu SVG olarak döner. Genişlik ve yükseklik milimetre; SVG yalnızca dikdörtgenlerden oluşur
    /// (metin içermez), bu yüzden sayfaya doğrudan eklenebilir.
    /// </summary>
    public static string Svg(string text, double widthMm, double heightMm)
    {
        var modules = ModuleCount(text);
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append(inv, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthMm}mm\" height=\"{heightMm}mm\" ")
          .Append(inv, $"viewBox=\"0 0 {modules} 100\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"Barkod\">");
        var x = QuietZone;
        foreach (var code in Encode(text))
        {
            var pattern = Patterns[code];
            for (var i = 0; i < pattern.Length; i++)
            {
                var w = pattern[i] - '0';
                if (i % 2 == 0) sb.Append(inv, $"<rect x=\"{x}\" y=\"0\" width=\"{w}\" height=\"100\"/>");
                x += w;
            }
        }
        return sb.Append("</svg>").ToString();
    }
}
