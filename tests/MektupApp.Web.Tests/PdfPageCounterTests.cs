using System.IO.Compression;
using System.Text;
using MektupApp.Application.Letters;

namespace MektupApp.Web.Tests;

public class PdfPageCounterTests
{
    private static byte[] Latin1(string s) => Encoding.Latin1.GetBytes(s);

    private static string PlainPdf(int pages)
    {
        var sb = new StringBuilder("%PDF-1.4\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        sb.Append($"2 0 obj << /Type /Pages /Kids [] /Count {pages} >> endobj\n");
        for (var i = 0; i < pages; i++)
            sb.Append($"{3 + i} 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >> endobj\n");
        return sb.Append("trailer << /Root 1 0 R >>\n%%EOF").ToString();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void Counts_page_objects(int pages) =>
        Assert.Equal(pages, PdfPageCounter.Count(Latin1(PlainPdf(pages))));

    [Fact]
    public void Counts_pages_inside_compressed_object_streams()
    {
        // PDF 1.5+: sayfa nesneleri sıkıştırılmış /ObjStm içinde.
        var objects = "<< /Type /Pages /Kids [4 0 R 5 0 R] /Count 2 >> << /Type /Page /Parent 3 0 R >> << /Type /Page /Parent 3 0 R >>";
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(Latin1(objects));
        var compressed = Encoding.Latin1.GetString(ms.ToArray());

        var pdf = "%PDF-1.7\n1 0 obj << /Type /Catalog /Pages 3 0 R >> endobj\n" +
                  $"2 0 obj << /Type /ObjStm /N 3 /First 10 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n" +
                  compressed + "\nendstream\nendobj\n%%EOF";
        Assert.Equal(2, PdfPageCounter.Count(Latin1(pdf)));
    }

    [Fact]
    public void Falls_back_to_pages_count_then_to_one()
    {
        Assert.Equal(7, PdfPageCounter.Count(Latin1("%PDF-1.4\n<< /Type /Pages /Count 7 >>\n%%EOF")));
        Assert.Equal(1, PdfPageCounter.Count(Latin1("%PDF-1.4\n%%EOF")));
        Assert.Equal(1, PdfPageCounter.Count([]));
    }

    [Fact]
    public void Corrupt_compressed_stream_does_not_throw() =>
        Assert.Equal(1, PdfPageCounter.Count(Latin1("%PDF-1.7\n<< /Type /ObjStm /Filter /FlateDecode >>\nstream\nnot-zlib-data\nendstream\n%%EOF")));
}
