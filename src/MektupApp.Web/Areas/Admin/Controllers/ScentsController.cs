using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Scents;
using MektupApp.Domain.Common;
using MektupApp.Domain.Scents;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public sealed class ScentForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Koku adı zorunludur."), StringLength(ScentOption.NameMax), Display(Name = "Koku adı")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 9999, ErrorMessage = "Sıra 0 ile 9999 arasında olmalıdır."), Display(Name = "Sıra")]
    public int SortOrder { get; set; }

    [Display(Name = "Aktif (müşteriler seçebilir)")]
    public bool IsActive { get; set; } = true;

    public bool IsUsed { get; set; }

    public SaveScentCommand ToCommand() => new(Name, SortOrder, IsActive);
}

public sealed record ScentsIndexViewModel(IReadOnlyList<ScentDto> Items, ScentForm NewScent);

/// <summary>
/// Kokular: mektup formundaki "Koku" listesi buradan gelir. Fiyat tektir (Fiyatlar → Koku).
/// Mektupta seçilmiş koku silinemez, pasife alınır.
/// </summary>
public class ScentsController(ScentService scents) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new ScentsIndexViewModel(await scents.ListAllAsync(ct), new ScentForm()));

    /// <summary>Listenin üstündeki hızlı ekleme formu.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "NewScent")] ScentForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await scents.CreateAsync(form.ToCommand(), ct);
                TempData["Success"] = $"\"{form.Name.Trim()}\" kokusu eklendi.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is DomainException or AppException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(nameof(Index), new ScentsIndexViewModel(await scents.ListAllAsync(ct), form));
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var s = await scents.GetAsync(id, ct);
        return View(new ScentForm { Id = s.Id, Name = s.Name, SortOrder = s.SortOrder, IsActive = s.IsActive, IsUsed = s.IsUsed });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, ScentForm form, CancellationToken ct)
    {
        form.Id = id;
        form.IsUsed = (await scents.GetAsync(id, ct)).IsUsed;
        if (!ModelState.IsValid) return View(form);
        try
        {
            await scents.UpdateAsync(id, form.ToCommand(), ct);
            TempData["Success"] = "Değişiklikler kaydedildi.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(form);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(Guid id, bool active, CancellationToken ct)
    {
        await scents.SetActiveAsync(id, active, ct);
        return BackWith(active ? "Koku aktif: müşteriler seçebilir." : "Koku pasife alındı: müşterilere gösterilmez.", Url.Action(nameof(Index))!);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await scents.DeleteAsync(id, ct);
            TempData["Success"] = "Koku silindi.";
        }
        catch (AppException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
