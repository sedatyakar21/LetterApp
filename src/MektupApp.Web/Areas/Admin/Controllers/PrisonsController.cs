using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Prisons;
using MektupApp.Domain.Common;
using MektupApp.Domain.Prisons;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class PrisonForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Kurum adı zorunludur."), StringLength(Prison.NameMax), Display(Name = "Kurum adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "İl zorunludur."), Display(Name = "İl")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "İlçe")]
    public string? District { get; set; }

    [StringLength(Prison.AddressMax), Display(Name = "Adres (mahalle, cadde/sokak, no)")]
    public string? AddressLine { get; set; }

    [RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli olmalıdır."), Display(Name = "Posta kodu")]
    public string? PostalCode { get; set; }

    [StringLength(60), Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Display(Name = "Aktif (mektup formunda listelenir)")]
    public bool IsActive { get; set; } = true;

    public SavePrisonCommand ToCommand() => new(Name, City, District, AddressLine, PostalCode, Phone, IsActive);
}

public sealed record PrisonsIndexViewModel(IReadOnlyList<PrisonDto> Items, string? City, string? Query);

/// <summary>
/// Cezaevi listesi: alıcı türü "Cezaevi" iken mektup formunda il seçilince bu kurumlar listelenir ve adres otomatik dolar.
/// Başlangıç verisi Ceza ve Tevkifevleri Genel Müdürlüğü sitesinden alınmıştır; değişiklikler buradan yapılır.
/// </summary>
public class PrisonsController(PrisonService prisons) : AdminControllerBase
{
    public async Task<IActionResult> Index([FromQuery(Name = "il")] string? city, [FromQuery(Name = "q")] string? query, CancellationToken ct) =>
        View(new PrisonsIndexViewModel(await prisons.SearchAsync(city, query, ct), city, query));

    public IActionResult Create([FromQuery(Name = "il")] string? city) => View("Edit", new PrisonForm { City = city ?? string.Empty });

    [HttpPost]
    public async Task<IActionResult> Create(PrisonForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        try
        {
            var id = await prisons.CreateAsync(form.ToCommand(), ct);
            TempData["Success"] = "Kurum eklendi.";
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
        var p = await prisons.GetAsync(id, ct);
        return View(new PrisonForm
        {
            Id = p.Id, Name = p.Name, City = p.City, District = p.District, AddressLine = p.AddressLine,
            PostalCode = string.IsNullOrEmpty(p.PostalCode) ? null : p.PostalCode, Phone = p.Phone, IsActive = p.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, PrisonForm form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View(form);
        try
        {
            await prisons.UpdateAsync(id, form.ToCommand(), ct);
            TempData["Success"] = "Değişiklikler kaydedildi.";
            return RedirectToAction(nameof(Index), new { il = form.City });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, string? il, CancellationToken ct)
    {
        await prisons.DeleteAsync(id, ct);
        TempData["Success"] = "Kurum silindi.";
        return RedirectToAction(nameof(Index), new { il });
    }
}
