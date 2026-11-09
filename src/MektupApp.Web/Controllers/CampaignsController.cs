using MektupApp.Application.Campaigns;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MektupApp.Web.Controllers;

/// <summary>Kampanya duyuruları. Yöneticiler taslakları "?onizleme=1" ile görebilir.</summary>
public class CampaignsController(CampaignService campaigns) : Controller
{
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet("kampanyalar")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Ended = await campaigns.ListEndedAsync(6, ct);
        return View(await campaigns.ListCurrentAsync(ct));
    }

    [HttpGet("kampanyalar/{slug}")]
    public async Task<IActionResult> Detail(string slug, bool onizleme = false, CancellationToken ct = default)
    {
        var c = await campaigns.GetBySlugAsync(slug, includeDrafts: onizleme && IsAdmin, ct);
        if (c is null) return NotFound();
        if (c.Slug != slug) return RedirectToActionPermanent(nameof(Detail), new { slug = c.Slug });

        ViewBag.Others = (await campaigns.ListActiveAsync(ct)).Where(x => x.Id != c.Id).Take(3).ToList();
        return View(c);
    }

    [HttpGet("kampanyalar/gorsel/{id:guid}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Cover(Guid id, CancellationToken ct)
    {
        var file = await campaigns.OpenCoverAsync(id, includeDrafts: IsAdmin, ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.Key, out var contentType);
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }
}
