using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Postcards;
using MektupApp.Domain.Common;
using MektupApp.Domain.Postcards;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class PostcardForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Kartpostal adı zorunludur."), StringLength(PostcardDesign.TitleMax), Display(Name = "Kartpostal adı")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kategori zorunludur."), StringLength(PostcardDesign.CategoryMax), Display(Name = "Kategori")]
    public string Category { get; set; } = string.Empty;

    [Range(0, 9999, ErrorMessage = "Sıra 0 ile 9999 arasında olmalıdır."), Display(Name = "Sıra")]
    public int SortOrder { get; set; }

    [Display(Name = "Aktif (müşteriler seçebilir)")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Görsel (ön yüz)")]
    public IFormFile? Image { get; set; }

    // Düzenleme ekranında gösterim için
    public DateTime? UpdatedAt { get; set; }
    public bool IsUsed { get; set; }

    public SavePostcardCommand ToCommand() => new(Title, Category, SortOrder, IsActive);
}

/// <summary>
/// Kartpostal kataloğu: görseli, adı, kategorisi ve sırası buradan girilir; müşteriler mektup yazarken aktif olanlardan seçer.
/// Mektupta seçilmiş kartpostal silinemez (siparişte görseli gerekir), pasife alınır.
/// </summary>
public class PostcardsController(PostcardService postcards) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await postcards.ListAllAsync(ct));

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        ViewBag.Categories = await postcards.ListCategoriesAsync(ct);
        return View("Edit", new PostcardForm());
    }

    [HttpPost, RequestSizeLimit(PostcardService.MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> Create(PostcardForm form, CancellationToken ct)
    {
        if (form.Image is null || form.Image.Length == 0)
            ModelState.AddModelError(nameof(form.Image), "Lütfen kartpostalın görselini seçin.");
        if (!ModelState.IsValid) return await EditViewAsync(form, ct);
        try
        {
            await using var stream = form.Image!.OpenReadStream();
            await postcards.CreateAsync(form.ToCommand(), stream, form.Image.FileName, form.Image.Length, ct);
            TempData["Success"] = $"\"{form.Title.Trim()}\" kartpostalı eklendi.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await EditViewAsync(form, ct);
        }
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var p = await postcards.GetAsync(id, ct);
        return await EditViewAsync(new PostcardForm
        {
            Id = p.Id, Title = p.Title, Category = p.Category, SortOrder = p.SortOrder, IsActive = p.IsActive,
            UpdatedAt = p.UpdatedAt, IsUsed = p.IsUsed
        }, ct);
    }

    /// <summary>Bilgiler; yeni görsel seçildiyse o da değiştirilir.</summary>
    [HttpPost, RequestSizeLimit(PostcardService.MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> Edit(Guid id, PostcardForm form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return await EditViewAsync(form, ct, reload: true);
        try
        {
            await postcards.UpdateAsync(id, form.ToCommand(), ct);
            if (form.Image is { Length: > 0 } image)
            {
                await using var stream = image.OpenReadStream();
                await postcards.ReplaceImageAsync(id, stream, image.FileName, image.Length, ct);
            }
            TempData["Success"] = "Değişiklikler kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await EditViewAsync(form, ct, reload: true);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(Guid id, bool active, CancellationToken ct)
    {
        await postcards.SetActiveAsync(id, active, ct);
        return BackWith(active ? "Kartpostal aktif: müşteriler seçebilir." : "Kartpostal pasife alındı: müşterilere gösterilmez.",
            Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await postcards.DeleteAsync(id, ct);
            TempData["Success"] = "Kartpostal silindi.";
        }
        catch (AppException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> EditViewAsync(PostcardForm form, CancellationToken ct, bool reload = false)
    {
        if (reload && form.Id is { } id)
        {
            var p = await postcards.GetAsync(id, ct);
            form.UpdatedAt = p.UpdatedAt;
            form.IsUsed = p.IsUsed;
        }
        ViewBag.Categories = await postcards.ListCategoriesAsync(ct);
        return View("Edit", form);
    }
}
