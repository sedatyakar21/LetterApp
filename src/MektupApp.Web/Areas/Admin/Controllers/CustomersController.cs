using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Loyalty;
using MektupApp.Domain.Customers;
using MektupApp.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

/// <summary>Müşteri yönetimi: listeleme, arama, engelleme ve yönetici yetkisi.</summary>
public class CustomersController(IAdminQueries queries, CustomerAdminService customers, LoyaltyService loyalty) : AdminControllerBase
{
    public async Task<IActionResult> Index(string? q, AcquisitionChannel? channel, Guid? referredBy, int page = 1, CancellationToken ct = default) =>
        View(new CustomersIndexViewModel
        {
            Result = await queries.SearchCustomersAsync(new CustomerSearchCriteria(q, channel, referredBy, page), ct),
            Query = q,
            Channel = channel,
            ReferredBy = referredBy,
            ReferredByName = referredBy is { } rid ? (await queries.GetCustomerAsync(rid, ct))?.FullName : null
        });

    public async Task<IActionResult> Details(Guid id, int page = 1, CancellationToken ct = default)
    {
        var customer = await queries.GetCustomerAsync(id, ct) ?? throw new AppException("Müşteri bulunamadı.");
        var letters = await queries.SearchLettersAsync(new LetterSearchCriteria(CustomerId: id, Page: page, PageSize: 10), ct);
        var points = await loyalty.GetAccountAsync(id, ct);
        return View(new CustomerDetailsViewModel
        {
            Customer = customer, Letters = letters, Points = points, IsSelf = id == CurrentUserId,
            BirthDate = await customers.GetBirthDateAsync(id, ct)
        });
    }

    [HttpPost]
    public async Task<IActionResult> BirthDate(Guid id, DateOnly? birthDate, CancellationToken ct)
    {
        await customers.SetBirthDateAsync(id, birthDate, ct);
        return BackWith(birthDate is null ? "Doğum tarihi silindi." : "Doğum tarihi güncellendi.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        await customers.BlockAsync(id, CurrentUserId, ct);
        return BackWith("Hesap askıya alındı; kullanıcının açık oturumu sonlandırıldı.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        await customers.UnblockAsync(id, CurrentUserId, ct);
        return BackWith("Hesap yeniden etkinleştirildi.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> GrantAdmin(Guid id, CancellationToken ct)
    {
        await customers.GrantAdminAsync(id, CurrentUserId, ct);
        return BackWith("Yönetici yetkisi verildi. Kullanıcının yeniden giriş yapması gerekecek.", DetailsUrl(id));
    }

    [HttpPost]
    public async Task<IActionResult> RevokeAdmin(Guid id, CancellationToken ct)
    {
        await customers.RevokeAdminAsync(id, CurrentUserId, ct);
        return BackWith("Yönetici yetkisi kaldırıldı.", DetailsUrl(id));
    }

    private string DetailsUrl(Guid id) => Url.Action(nameof(Details), new { id })!;
}
