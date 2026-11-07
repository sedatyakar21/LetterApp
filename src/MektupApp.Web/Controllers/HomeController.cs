using System.Diagnostics;
using MektupApp.Application.Pricing;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

public class HomeController(PricingService pricing, MektupApp.Application.Blog.BlogService blog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.LatestPosts = await blog.LatestAsync(3, ct);
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
