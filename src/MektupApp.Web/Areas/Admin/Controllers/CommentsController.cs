using MektupApp.Application.Admin;
using MektupApp.Application.Reviews;
using MektupApp.Domain.Reviews;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class CommentsIndexViewModel
{
    /// <summary>"musteri" (müşteri yorumları) veya "blog".</summary>
    public required string Tab { get; init; }
    public ModerationStatus? Status { get; init; }
    public PagedResult<ReviewDto>? Reviews { get; init; }
    public PagedResult<BlogCommentDto>? BlogComments { get; init; }
    public int PendingReviews { get; init; }
    public int PendingBlogComments { get; init; }
}

/// <summary>Müşteri yorumları ve blog yorumlarının onaylanması.</summary>
public class CommentsController(ReviewService reviews, BlogCommentService blogComments) : AdminControllerBase
{
    private const int PageSize = 20;

    /// <param name="durum">Boşsa onay bekleyenler; "tumu" tüm yorumlar.</param>
    public async Task<IActionResult> Index(string tab = "musteri", string? durum = null, int page = 1, CancellationToken ct = default)
    {
        ModerationStatus? status = durum switch
        {
            "tumu" => null,
            null or "" => ModerationStatus.Pending,
            _ => Enum.TryParse<ModerationStatus>(durum, true, out var s) ? s : ModerationStatus.Pending
        };
        var isBlog = tab == "blog";
        return View(new CommentsIndexViewModel
        {
            Tab = isBlog ? "blog" : "musteri",
            Status = status,
            Reviews = isBlog ? null : await reviews.ListForModerationAsync(status, page, PageSize, ct),
            BlogComments = isBlog ? await blogComments.ListForModerationAsync(status, page, PageSize, ct) : null,
            PendingReviews = await reviews.CountPendingAsync(ct),
            PendingBlogComments = await blogComments.CountPendingAsync(ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(Guid id, string kind, CancellationToken ct)
    {
        if (kind == "blog") await blogComments.ApproveAsync(id, ct); else await reviews.ApproveAsync(id, ct);
        return BackWith("Yorum yayınlandı.", Url.Action(nameof(Index), new { tab = kind })!);
    }

    [HttpPost]
    public async Task<IActionResult> Reject(Guid id, string kind, string? reason, CancellationToken ct)
    {
        if (kind == "blog") await blogComments.RejectAsync(id, reason, ct); else await reviews.RejectAsync(id, reason, ct);
        return BackWith("Yorum reddedildi.", Url.Action(nameof(Index), new { tab = kind })!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, string kind, CancellationToken ct)
    {
        if (kind == "blog") await blogComments.DeleteAsync(id, ct); else await reviews.DeleteAsync(id, ct);
        return BackWith("Yorum silindi.", Url.Action(nameof(Index), new { tab = kind })!);
    }
}
