using MektupApp.Web.Infrastructure;

namespace MektupApp.Web.Tests;

/// <summary>Blog içeriği yönetici tarafından yazılsa da HTML/JS enjeksiyonuna kapalı olmalı.</summary>
public class MarkdownRendererTests
{
    private static string Render(string md) => MarkdownRenderer.ToHtml(md).Value!;

    [Fact]
    public void Renders_basic_markdown()
    {
        var html = Render("## Başlık\n\n**kalın** ve *italik*\n\n- madde");
        Assert.Contains("<h2", html);
        Assert.Contains("<strong>kalın</strong>", html);
        Assert.Contains("<em>italik</em>", html);
        Assert.Contains("<li>madde</li>", html);
    }

    [Fact]
    public void Raw_html_is_escaped_not_executed()
    {
        var html = Render("Merhaba <script>alert(1)</script> <img src=x onerror=alert(1)>");
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData("[tıkla](javascript:alert(1))")]
    [InlineData("[tıkla](JavaScript:alert(1))")]
    [InlineData("[tıkla](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("![g](javascript:alert(1))")]
    [InlineData("[tıkla](//evil.example/x)")]
    public void Unsafe_link_targets_are_neutralised(string md)
    {
        var html = Render(md);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html);
        Assert.DoesNotContain("//evil.example", html);
        Assert.Contains("href=\"#\"", html.Replace("src=\"#\"", "href=\"#\""));
    }

    [Fact]
    public void External_links_open_safely_and_internal_links_are_kept()
    {
        var html = Render("[PTT](https://www.ptt.gov.tr) ve [yaz](/mektup-yaz)");
        Assert.Contains("href=\"https://www.ptt.gov.tr\"", html);
        Assert.Contains("rel=\"nofollow noopener\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("href=\"/mektup-yaz\"", html);
    }
}
