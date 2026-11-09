using MektupApp.Application.Reviews;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

/// <summary>Müşteri yorumları sayfası ve teslim edilen mektuba yorum yazma.</summary>
public class ReviewsController(ReviewService reviews) : Controller
{
    private const int PageSize = 12;

    [HttpGet("yorumlar")]
    public async Task<IActionResult> Index(int sayfa = 1, CancellationToken ct = default)
    {
        ViewBag.Summary = await reviews.GetSummaryAsync(ct);
        return View(await reviews.ListApprovedAsync(sayfa, PageSize, ct));
    }

    [Authorize, HttpPost("mektup/{id:guid}/yorum")]
    public async Task<IActionResult> Submit(Guid id, int rating, string? comment, CancellationToken ct)
    {
        await reviews.SubmitAsync(User.GetCustomerId(), id, rating, comment ?? string.Empty, ct);
        TempData["Success"] = "Teşekkürler! Yorumunuz onaylandıktan sonra yayınlanacak. 💌";
        return RedirectToAction("Details", "Letters", new { id }, "review");
    }
}
