using System.Diagnostics;
using MektupApp.Application.Pricing;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

public class HomeController(PricingService pricing, MektupApp.Application.Blog.BlogService blog,
    MektupApp.Application.Campaigns.CampaignService campaigns, MektupApp.Application.Reviews.ReviewService reviews) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.LatestPosts = await blog.LatestAsync(3, ct);
        ViewBag.ActiveCampaigns = (await campaigns.ListActiveAsync(ct)).Take(3).ToList();
        ViewBag.Reviews = (await reviews.ListApprovedAsync(1, 3, ct)).Items;
        ViewBag.ReviewSummary = await reviews.GetSummaryAsync(ct);
        return View(await pricing.GetCurrentAsync(ct));
    }

    [HttpGet("sss")]
    public async Task<IActionResult> Faq(CancellationToken ct) => View(await pricing.GetCurrentAsync(ct));

    [HttpGet("uyelik-sozlesmesi")]
    public IActionResult Terms() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
