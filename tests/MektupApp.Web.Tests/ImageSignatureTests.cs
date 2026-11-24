using System.Text;
using MektupApp.Application.Postcards;

namespace MektupApp.Web.Tests;

public class ImageSignatureTests
{
    private static readonly byte[] Webp = [.. "RIFF"u8, 0x24, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16];

    [Fact] public void Webp_is_accepted() => Assert.True(ImageSignature.Matches(Webp, ".webp"));
    [Fact] public void Png_is_accepted() => Assert.True(ImageSignature.Matches(Png, ".PNG"));
    [Fact] public void Jpeg_is_accepted() => Assert.True(ImageSignature.Matches(Jpeg, ".jpeg"));

    [Fact]
    public void Html_renamed_as_image_is_rejected()
    {
        var html = Encoding.ASCII.GetBytes("<html><script>alert(1)</script>");
        Assert.False(ImageSignature.Matches(html, ".jpg"));
        Assert.False(ImageSignature.Matches(html, ".webp"));
    }

    [Fact] public void Extension_must_match_content() => Assert.False(ImageSignature.Matches(Png, ".webp"));
    [Fact] public void Pdf_is_not_an_image() => Assert.False(ImageSignature.Matches("%PDF-1.7"u8, ".pdf"));
}
