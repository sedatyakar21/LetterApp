using MektupApp.Application.Legal;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

/// <summary>Yönetim panelinden girilen sözleşmeler ve yasal metinler.</summary>
public class LegalController(LegalDocumentService documents) : Controller
{
    /// <param name="onizleme">Yönetici, yayında olmayan taslağı görebilir.</param>
    [HttpGet("sozlesme/{slug}")]
    public async Task<IActionResult> Show(string slug, bool onizleme, CancellationToken ct)
    {
        var doc = await documents.GetPublishedAsync(slug, includeDrafts: onizleme && User.IsInRole("Admin"), ct);
        return doc is null ? NotFound() : View(doc);
    }

    /// <summary>Tüm yayındaki belgeler.</summary>
    [HttpGet("sozlesmeler")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await documents.ListPublishedAsync(footerOnly: false, ct));
}
