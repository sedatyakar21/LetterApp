using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Web.Infrastructure;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

public class AccountController(AccountService accounts) : Controller
{
    [HttpGet("giris")]
    public IActionResult Login(string? returnUrl) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost("giris")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        AuthenticatedCustomer? customer;
        try { customer = await accounts.AuthenticateAsync(model.Email, model.Password, ct); }
        catch (AppException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        if (customer is null)
        {
            ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
            return View(model);
        }

        await SignInAsync(customer);
        return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Letters");
    }

    /// <param name="ref">Davet linkinden gelen referans no (/kayit?ref=MK...); "Arkadaş" seçili gelir.</param>
    [HttpGet("kayit")]
    public IActionResult Register([FromQuery(Name = "ref")] string? @ref) =>
        View(string.IsNullOrWhiteSpace(@ref)
            ? new RegisterViewModel()
            : new RegisterViewModel { Channel = AcquisitionChannel.Friend, ReferralCode = @ref.Trim() });

    [HttpPost("kayit")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!model.AcceptTerms)
            ModelState.AddModelError(nameof(model.AcceptTerms), "Üyelik sözleşmesini onaylamalısınız.");
        if (model.Channel is null)
            ModelState.AddModelError(nameof(model.Channel), "Lütfen bizi nereden duyduğunuzu seçin.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            var customer = await accounts.RegisterAsync(
                new RegisterCommand(model.FullName, model.Email, model.Phone, model.Password,
                    model.Channel, model.Channel == AcquisitionChannel.Friend ? model.ReferralCode : null, model.BirthDate), ct);
            await SignInAsync(customer);
            TempData["Success"] = "Hoş geldiniz! İlk mektubunuzu yazmaya başlayabilirsiniz.";
            return RedirectToAction("Write", "Letters");
        }
        catch (Exception ex) when (ex is DomainException or AppException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize, HttpGet("hesabim")]
    public async Task<IActionResult> Profile(CancellationToken ct) =>
        View(await accounts.GetAccountAsync(CurrentUserId, ct) ?? throw new AppException("Üyelik bulunamadı."));

    [Authorize, HttpPost("hesabim/dogum-tarihi")]
    public async Task<IActionResult> BirthDate(DateOnly? birthDate, CancellationToken ct)
    {
        if (birthDate is null) throw new AppException("Lütfen doğum tarihinizi seçin.");
        await accounts.SetBirthDateAsync(CurrentUserId, birthDate.Value, ct);
        TempData["Success"] = "Doğum tarihiniz kaydedildi. 🎂 Doğum günü kampanyalarından yararlanabilirsiniz.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost("hesabim/google/kaldir")]
    public async Task<IActionResult> UnlinkGoogle(CancellationToken ct)
    {
        await accounts.UnlinkGoogleAsync(CurrentUserId, ct);
        TempData["Success"] = "Google hesabınızın bağlantısı kaldırıldı.";
        return RedirectToAction(nameof(Profile));
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize, HttpPost("cikis")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private Task SignInAsync(AuthenticatedCustomer customer) => HttpContext.SignInCustomerAsync(customer);
}
