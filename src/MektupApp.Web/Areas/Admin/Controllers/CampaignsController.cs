using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Campaigns;
using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class CampaignForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(Campaign.TitleMax), Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [StringLength(MektupApp.Domain.Common.Slug.MaxLength), Display(Name = "Adres (slug)")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Kısa açıklama zorunludur."), StringLength(Campaign.SummaryMax), Display(Name = "Kısa açıklama")]
    public string Summary { get; set; } = string.Empty;

    [Display(Name = "Detaylı açıklama / koşullar (Markdown)")]
    public string? DescriptionMarkdown { get; set; }

    [StringLength(Campaign.BadgeMax), Display(Name = "Rozet")]
    public string? BadgeText { get; set; }

    [StringLength(Campaign.CouponMax), Display(Name = "Kupon kodu")]
    public string? CouponCode { get; set; }

    [Required(ErrorMessage = "Başlangıç tarihi zorunludur."), Display(Name = "Başlangıç")]
    public DateOnly StartDate { get; set; } = TurkeyCalendar.Today;

    [Display(Name = "Bitiş (dahil)")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "İndirim türü")]
    public CampaignDiscountType DiscountType { get; set; }

    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true),
     Display(Name = "İndirim değeri")]
    public decimal DiscountValue { get; set; }

    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true),
     Display(Name = "En düşük sepet tutarı (₺)")]
    public decimal? MinOrderAmount { get; set; }

    [Display(Name = "Kimlere uygulanır")]
    public CampaignAudience Audience { get; set; }

    [Range(0, 365), Display(Name = "Gün")]
    public int? AudienceDays { get; set; }

    [Range(1, 100), Display(Name = "Üye başına kullanım")]
    public int? UsageLimitPerCustomer { get; set; }

    public bool IsPublished { get; set; }
    public bool HasCover { get; set; }
    public CampaignPhase Phase { get; set; }

    public SaveCampaignCommand ToCommand() =>
        new(Title, Slug, Summary, DescriptionMarkdown, BadgeText, CouponCode, StartDate, EndDate,
            DiscountType, DiscountValue, MinOrderAmount, Audience, AudienceDays, UsageLimitPerCustomer);
}

/// <summary>Kampanya yönetimi: duyuru ve ödeme ekranında uygulanan indirim kuralı.</summary>
public class CampaignsController(CampaignService campaigns) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await campaigns.ListAllAsync(ct));

    public IActionResult Create() => View("Edit", new CampaignForm { EndDate = TurkeyCalendar.Today.AddDays(14) });

    [HttpPost]
    public async Task<IActionResult> Create(CampaignForm form, bool publish, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        try
        {
            var id = await campaigns.CreateAsync(form.ToCommand(), publish, ct);
            TempData["Success"] = publish ? "Kampanya yayınlandı." : "Kampanya taslak olarak kaydedildi.";
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
        var c = await campaigns.GetAsync(id, ct);
        return View(new CampaignForm
        {
            Id = c.Id, Title = c.Title, Slug = c.Slug, Summary = c.Summary, DescriptionMarkdown = c.DescriptionMarkdown,
            BadgeText = c.BadgeText, CouponCode = c.CouponCode, StartDate = c.StartDate, EndDate = c.EndDate,
            IsPublished = c.IsPublished, HasCover = c.HasCover, Phase = c.Phase,
            DiscountType = c.DiscountType, DiscountValue = c.DiscountValue, MinOrderAmount = c.MinOrderAmount,
            Audience = c.Audience, AudienceDays = c.AudienceDays, UsageLimitPerCustomer = c.UsageLimitPerCustomer
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, CampaignForm form, bool publish, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View(await WithStateAsync(form, ct));
        try
        {
            await campaigns.UpdateAsync(id, form.ToCommand(), ct);
            if (publish) await campaigns.PublishAsync(id, ct);
            TempData["Success"] = publish ? "Kaydedildi ve yayınlandı." : "Değişiklikler kaydedildi.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await WithStateAsync(form, ct));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        await campaigns.PublishAsync(id, ct);
        return BackWith("Kampanya yayınlandı.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        await campaigns.UnpublishAsync(id, ct);
        return BackWith("Kampanya yayından kaldırıldı.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        TempData["Success"] = "Kampanya silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Cover(Guid id, IFormFile? cover, CancellationToken ct)
    {
        if (cover is null) throw new AppException("Lütfen bir görsel seçin.");
        await using var stream = cover.OpenReadStream();
        await campaigns.SetCoverAsync(id, stream, cover.FileName, cover.Length, ct);
        return BackWith("Kapak görseli güncellendi.", Url.Action(nameof(Edit), new { id })!);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        await campaigns.RemoveCoverAsync(id, ct);
        return BackWith("Kapak görseli kaldırıldı.", Url.Action(nameof(Edit), new { id })!);
    }

    private async Task<CampaignForm> WithStateAsync(CampaignForm form, CancellationToken ct)
    {
        var current = await campaigns.GetAsync(form.Id!.Value, ct);
        form.IsPublished = current.IsPublished; form.HasCover = current.HasCover; form.Phase = current.Phase;
        return form;
    }
}
