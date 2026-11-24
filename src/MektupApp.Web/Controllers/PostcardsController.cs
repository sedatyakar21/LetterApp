using MektupApp.Application.Postcards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MektupApp.Web.Controllers;

/// <summary>Kartpostal görselleri (mektup formu, sipariş sayfaları, mobil uygulama ve baskı sayfası kullanır).</summary>
public class PostcardsController(PostcardService postcards) : Controller
{
    /// <summary>Herkese açık; adres görsel değişince değişsin diye "v" (güncelleme zamanı) parametresiyle verilir.</summary>
    [HttpGet("kartpostal/{id:guid}/gorsel")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Image(Guid id, CancellationToken ct)
    {
        var file = await postcards.OpenImageAsync(id, ct);
        if (file is null) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(file.Value.Key, out var contentType);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Value.Stream, contentType ?? "application/octet-stream");
    }

    /// <summary>Görsel adresi (önbellek, görsel değişince tazelensin diye sürüm parametresiyle).</summary>
    public static string ImageUrl(IUrlHelper url, Guid id, DateTime? updatedAt = null) =>
        url.Action(nameof(Image), "Postcards", new { area = "", id, v = updatedAt?.Ticks.ToString("x") })!;
}
