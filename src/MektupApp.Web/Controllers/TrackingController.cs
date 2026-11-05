using MektupApp.Application.Letters;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

public class TrackingController(LetterService letters) : Controller
{
    [HttpGet("takip")]
    public async Task<IActionResult> Index(string? code, CancellationToken ct)
    {
        ViewBag.Code = code;
        if (string.IsNullOrWhiteSpace(code)) return View(model: null);

        var result = await letters.TrackAsync(code, ct);
        if (result is null) ViewBag.NotFound = true;
        return View(result);
    }
}
