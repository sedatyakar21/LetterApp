using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Blog içeriğini (Markdown) güvenli HTML'e çevirir:
///  - Ham HTML kapalıdır (yazıya &lt;script&gt; vb. eklenemez, metin olarak görünür).
///  - Bağlantı / görsel adreslerinde sadece http(s), mailto ve site içi (/...) adreslere izin verilir;
///    javascript:, data: gibi adresler etkisizleştirilir.
///  - Dış bağlantılar yeni sekmede ve rel="nofollow noopener" ile açılır.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()   // tablo, görev listesi, otomatik bağlantı vb.
        .UseGenericAttributes()
        .DisableHtml()
        .Build();

    public static HtmlString ToHtml(string? markdown)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
                continue;
            }
            if (!link.IsImage && IsExternal(link.Url))
            {
                var attrs = link.GetAttributes();
                attrs.AddPropertyIfNotExist("target", "_blank");
                attrs.AddPropertyIfNotExist("rel", "nofollow noopener");
            }
            if (link.IsImage)
                link.GetAttributes().AddPropertyIfNotExist("loading", "lazy");
        }
        foreach (var auto in document.Descendants<AutolinkInline>())
            if (!IsSafeUrl(auto.Url)) auto.Url = "#";

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return new HtmlString(writer.ToString());
    }

    internal static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var u = url.Trim();
        if (u.StartsWith('/') && !u.StartsWith("//")) return true;  // site içi
        if (u.StartsWith('#')) return true;                           // sayfa içi
        return Uri.TryCreate(u, UriKind.Absolute, out var abs) &&
               (abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeMailto);
    }

    private static bool IsExternal(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
