using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MektupApp.Application.Legal;
using MektupApp.Domain.Common;
using MektupApp.Domain.Legal;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class LegalDocumentForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(LegalDocument.TitleMax)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [StringLength(LegalDocument.SlugMax), Display(Name = "Adres")]
    public string? Slug { get; set; }

    [StringLength(LegalDocument.ContentMax), Display(Name = "Metin (Markdown)")]
    public string? ContentMarkdown { get; set; }

    [Display(Name = "Alt bilgide göster")]
    public bool ShowInFooter { get; set; } = true;

    [Range(0, 999), Display(Name = "Sıra")]
    public int SortOrder { get; set; } = 100;

    // Yalnızca gösterim
    public bool IsPublished { get; set; }
    public bool IsSystem { get; set; }
    public int Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public SaveLegalDocumentCommand ToCommand() => new(Title, Slug, ContentMarkdown ?? string.Empty, ShowInFooter, SortOrder);
}

/// <summary>Sözleşmeler ve yasal metinler (üyelik, mesafeli satış, ön bilgilendirme, KVKK, iptal / iade …).</summary>
public class LegalController(LegalDocumentService documents) : AdminControllerBase
{
    private string AdminName => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "yönetici";

    public async Task<IActionResult> Index(CancellationToken ct) => View(await documents.ListAllAsync(ct));

    public IActionResult Create() => View("Edit", new LegalDocumentForm());

    [HttpPost]
    public async Task<IActionResult> Create(LegalDocumentForm form, bool publish, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        try
        {
            var id = await documents.CreateAsync(form.ToCommand(), publish, AdminName, ct);
            TempData["Success"] = publish ? "Belge yayınlandı." : "Belge taslak olarak kaydedildi.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Edit", form);
        }
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var d = await documents.GetAsync(id, ct);
        return View(new LegalDocumentForm
        {
            Id = d.Id, Title = d.Title, Slug = d.Slug, ContentMarkdown = d.ContentMarkdown, ShowInFooter = d.ShowInFooter,
            SortOrder = d.SortOrder, IsPublished = d.IsPublished, IsSystem = d.IsSystem, Version = d.Version,
            UpdatedAt = d.UpdatedAt, UpdatedBy = d.UpdatedBy
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, LegalDocumentForm form, bool publish, CancellationToken ct)
    {
        form.Id = id;
        var current = await documents.GetAsync(id, ct);
        form.IsPublished = current.IsPublished;
        form.IsSystem = current.IsSystem;
        form.Version = current.Version;
        form.UpdatedAt = current.UpdatedAt;
        form.UpdatedBy = current.UpdatedBy;
        if (!ModelState.IsValid) return View(form);
        try
        {
            await documents.UpdateAsync(id, form.ToCommand(), publish, AdminName, ct);
            TempData["Success"] = publish ? "Belge kaydedildi ve yayınlandı." : "Belge kaydedildi.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        await documents.SetPublishedAsync(id, true, ct);
        return BackWith("Belge yayınlandı.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        await documents.SetPublishedAsync(id, false, ct);
        return BackWith("Belge yayından kaldırıldı; sitede görünmez.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return BackWith("Belge silindi.", Url.Action(nameof(Index))!);
    }

    /// <summary>Düzenleme ekranındaki önizleme (sitedeki ile aynı güvenli Markdown dönüştürücüsü).</summary>
    [HttpPost]
    public IActionResult Preview(string? markdown) =>
        Content(MarkdownRenderer.ToHtml(markdown).Value ?? string.Empty, "text/html; charset=utf-8");
}
