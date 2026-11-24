using MektupApp.Web.Infrastructure;

namespace MektupApp.Web.Tests;

public class Code128Tests
{
    [Fact]
    public void Pattern_table_is_well_formed()
    {
        Assert.Equal(107, Code128.Patterns.Count);
        // Her simge 11 modül (3 çubuk + 3 boşluk); bitiş 13 modül (4 çubuk + 3 boşluk).
        for (var i = 0; i < 106; i++)
        {
            Assert.Equal(6, Code128.Patterns[i].Length);
            Assert.Equal(11, Code128.Patterns[i].Sum(c => c - '0'));
        }
        Assert.Equal(13, Code128.Patterns[106].Sum(c => c - '0'));
        // Tüm desenler farklı olmalı (okuyucu her simgeyi ayırt edebilsin).
        Assert.Equal(107, Code128.Patterns.Distinct().Count());
    }

    [Fact]
    public void Encodes_start_b_data_checksum_and_stop()
    {
        // "PJJ123C": değerler 48 42 42 17 18 19 35;
        // kontrol = (104 + 48·1 + 42·2 + 42·3 + 17·4 + 18·5 + 19·6 + 35·7) mod 103 = 879 mod 103 = 55
        var codes = Code128.Encode("PJJ123C");
        Assert.Equal([104, 48, 42, 42, 17, 18, 19, 35, 55, 106], codes);
    }

    [Fact]
    public void Tracking_code_produces_svg_with_bars_only()
    {
        var svg = Code128.Svg("RR123456789TR", 50, 12);
        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 " + Code128.ModuleCount("RR123456789TR") + " 100\"", svg);
        Assert.DoesNotContain("<text", svg);
        // 13 karakter + başlangıç + kontrol = 15 simge × 3 çubuk + bitiş 4 çubuk = 49 çubuk
        Assert.Equal(49, svg.Split("<rect").Length - 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Çay")]
    public void Rejects_empty_or_unsupported_text(string text) =>
        Assert.Throws<ArgumentException>(() => Code128.Encode(text));
}
