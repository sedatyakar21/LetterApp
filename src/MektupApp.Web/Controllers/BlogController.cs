using MektupApp.Application.Blog;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MektupApp.Web.Controllers;

/// <summary>Herkese açık blog. Yöneticiler taslakları "?onizleme=1" ile görebilir.</summary>
public class BlogController(BlogService blog, MektupApp.Application.Reviews.BlogCommentService comments) : Controller
{
    private const int PageSize = 9;
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet("blog")]
    public async Task<IActionResult> Index(int sayfa = 1, CancellationToken ct = default) =>
        View(await blog.ListPublishedAsync(sayfa, PageSize, ct));

    [HttpGet("blog/{slug}")]
    public async Task<IActionResult> Post(string slug, bool onizleme = false, CancellationToken ct = default)
    {
        var post = await blog.GetBySlugAsync(slug, includeDrafts: onizleme && IsAdmin, ct);
        if (post is null) return NotFound();
        if (post.Slug != slug) return RedirectToActionPermanent(nameof(Post), new { slug = post.Slug }); // büyük harf vb.

        ViewBag.Related = (await blog.LatestAsync(4, ct)).Where(p => p.Id != post.Id).Take(3).ToList();
        ViewBag.Comments = await comments.ListApprovedAsync(post.Id, ct);
        ViewBag.MyPending = User.Identity?.IsAuthenticated == true
            ? await comments.ListMyPendingAsync(post.Id, User.GetCustomerId(), ct)
            : [];
        return View(post);
    }

    [Authorize, HttpPost("blog/{slug}/yorum")]
    public async Task<IActionResult> Comment(string slug, string? body, CancellationToken ct)
    {
        var post = await blog.GetBySlugAsync(slug, includeDrafts: false, ct);
        if (post is null) return NotFound();
        await comments.SubmitAsync(post.Id, User.GetCustomerId(), body ?? string.Empty, ct);
        TempData["Success"] = "Yorumunuz alındı; onaylandıktan sonra yayınlanacak.";
        return RedirectToAction(nameof(Post), null, new { slug = post.Slug }, "yorumlar");
    }

    [HttpGet("blog/gorsel/{id:guid}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)] // taslak kapakları ara önbelleklere düşmesin
    public async Task<IActionResult> Cover(Guid id, CancellationToken ct)
    {
        var file = await blog.OpenCoverAsync(id, includeDrafts: IsAdmin, ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.Key, out var contentType);
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }
}
