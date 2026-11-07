using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Blog;
using MektupApp.Domain.Blog;
using MektupApp.Domain.Common;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class BlogPostForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(BlogPost.TitleMax)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [StringLength(BlogSlug.MaxLength), Display(Name = "Adres (slug)")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Özet zorunludur."), StringLength(BlogPost.ExcerptMax)]
    [Display(Name = "Özet")]
    public string Excerpt { get; set; } = string.Empty;

    [Required(ErrorMessage = "İçerik zorunludur."), Display(Name = "İçerik (Markdown)")]
    public string ContentMarkdown { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yazar zorunludur."), StringLength(100), Display(Name = "Yazar")]
    public string AuthorName { get; set; } = "BiMektup";

    // Sadece düzenleme ekranında gösterilir
    public BlogPostStatus Status { get; set; }
    public bool HasCover { get; set; }
    public DateTime? PublishedAt { get; set; }

    public SaveBlogPostCommand ToCommand() => new(Title, Slug, Excerpt, ContentMarkdown, AuthorName);
}

/// <summary>Blog yazısı yönetimi.</summary>
public class BlogController(BlogService blog) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await blog.ListAllAsync(ct));

    public IActionResult Create() => View("Edit", new BlogPostForm { AuthorName = User.Identity?.Name ?? "BiMektup" });

    /// <param name="publish">"Kaydet ve Yayınla" butonuyla true gelir.</param>
    [HttpPost]
    public async Task<IActionResult> Create(BlogPostForm form, bool publish, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        try
        {
            var id = await blog.CreateAsync(form.ToCommand(), publish, ct);
            TempData["Success"] = publish ? "Yazı yayınlandı." : "Taslak kaydedildi.";
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
        var p = await blog.GetAsync(id, ct);
        return View(new BlogPostForm
        {
            Id = p.Id, Title = p.Title, Slug = p.Slug, Excerpt = p.Excerpt, ContentMarkdown = p.ContentMarkdown,
            AuthorName = p.AuthorName, Status = p.Status, HasCover = p.HasCover, PublishedAt = p.PublishedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, BlogPostForm form, bool publish, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View(form);
        try
        {
            await blog.UpdateAsync(id, form.ToCommand(), ct);
            if (publish) await blog.PublishAsync(id, ct);
            TempData["Success"] = publish ? "Kaydedildi ve yayınlandı." : "Değişiklikler kaydedildi.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var current = await blog.GetAsync(id, ct);
            form.Status = current.Status; form.HasCover = current.HasCover; form.PublishedAt = current.PublishedAt;
            return View(form);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        await blog.PublishAsync(id, ct);
        return BackWith("Yazı yayınlandı.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        await blog.UnpublishAsync(id, ct);
        return BackWith("Yazı yayından kaldırıldı (taslağa alındı).", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await blog.DeleteAsync(id, ct);
        TempData["Success"] = "Yazı silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Cover(Guid id, IFormFile? cover, CancellationToken ct)
    {
        if (cover is null) throw new AppException("Lütfen bir görsel seçin.");
        await using var stream = cover.OpenReadStream();
        await blog.SetCoverAsync(id, stream, cover.FileName, cover.Length, ct);
        return BackWith("Kapak görseli güncellendi.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        await blog.RemoveCoverAsync(id, ct);
        return BackWith("Kapak görseli kaldırıldı.", Url.Action(nameof(Edit), new { id })!);
    }

    /// <summary>Düzenleme ekranındaki canlı önizleme: Markdown → güvenli HTML.</summary>
    [HttpPost]
    public IActionResult Preview(string? markdown) =>
        Content(MarkdownRenderer.ToHtml(markdown).Value ?? string.Empty, "text/html; charset=utf-8");
}
