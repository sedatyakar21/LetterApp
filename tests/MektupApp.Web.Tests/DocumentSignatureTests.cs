using System.Text;
using MektupApp.Application.Letters;

namespace MektupApp.Web.Tests;

public class DocumentSignatureTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46];

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".PDF")]
    public void Real_pdf_is_accepted(string ext) => Assert.True(DocumentSignature.Matches(Pdf, ext));

    [Fact]
    public void Pdf_header_may_follow_leading_bytes() =>
        Assert.True(DocumentSignature.Matches([.. new byte[] { 0xEF, 0xBB, 0xBF }, .. Pdf], ".pdf"));

    [Fact]
    public void Images_are_accepted_by_signature()
    {
        Assert.True(DocumentSignature.Matches(Png, ".png"));
        Assert.True(DocumentSignature.Matches(Jpeg, ".jpg"));
        Assert.True(DocumentSignature.Matches(Jpeg, ".jpeg"));
    }

    [Fact]
    public void Renamed_or_unknown_files_are_rejected()
    {
        Assert.False(DocumentSignature.Matches(Encoding.ASCII.GetBytes("<html><script>"), ".pdf")); // HTML'in adı .pdf yapılmış
        Assert.False(DocumentSignature.Matches(Encoding.ASCII.GetBytes("MZ\x90\0"), ".pdf"));       // exe
        Assert.False(DocumentSignature.Matches(Pdf, ".png"));                                       // uzantı uyuşmuyor
        Assert.False(DocumentSignature.Matches(Jpeg, ".png"));
        Assert.False(DocumentSignature.Matches(Png, ".docx"));                                      // izin verilmeyen tür
        Assert.False(DocumentSignature.Matches([], ".pdf"));
    }
}
