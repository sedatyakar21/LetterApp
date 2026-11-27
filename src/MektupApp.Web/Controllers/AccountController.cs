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

public class AccountController(AccountService accounts, ContactChangeService contactChanges) : Controller
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
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        ViewData["PendingContact"] = await contactChanges.ListPendingAsync(CurrentUserId, ct);
        return View(await accounts.GetAccountAsync(CurrentUserId, ct) ?? throw new AppException("Üyelik bulunamadı."));
    }

    [Authorize, HttpPost("hesabim/dogum-tarihi")]
    public async Task<IActionResult> BirthDate(DateOnly? birthDate, CancellationToken ct)
    {
        if (birthDate is null) throw new AppException("Lütfen doğum tarihinizi seçin.");
        await accounts.SetBirthDateAsync(CurrentUserId, birthDate.Value, ct);
        TempData["Success"] = "Doğum tarihiniz kaydedildi. 🎂 Doğum günü kampanyalarından yararlanabilirsiniz.";
        return RedirectToAction(nameof(Profile));
    }

    // ---------------- E-posta / cep telefonu değişikliği (yeni adrese onay kodu) ----------------

    [Authorize, HttpPost("hesabim/eposta")]
    public Task<IActionResult> RequestEmailChange(string? email, string? currentPassword, CancellationToken ct) =>
        ContactStepAsync(ContactChangeKind.Email, async () =>
        {
            var p = await contactChanges.RequestEmailChangeAsync(CurrentUserId, email, currentPassword, ct);
            return $"Onay kodu {p.Destination} adresine gönderildi. Kodu aşağıya girin.";
        }, email);

    [Authorize, HttpPost("hesabim/telefon")]
    public Task<IActionResult> RequestPhoneChange(string? phone, string? currentPassword, CancellationToken ct) =>
        ContactStepAsync(ContactChangeKind.Phone, async () =>
        {
            var p = await contactChanges.RequestPhoneChangeAsync(CurrentUserId, phone, currentPassword, ct);
            return $"Onay kodu {p.Destination} numarasına SMS ile gönderildi. Kodu aşağıya girin.";
        }, phone);

    [Authorize, HttpPost("hesabim/iletisim/onayla")]
    public Task<IActionResult> ConfirmContactChange(ContactChangeKind kind, string? code, CancellationToken ct) =>
        ContactStepAsync(kind, async () =>
        {
            // E-posta değiştiyse oturum çerezi yeni adresle yenilenir (çıkış yapmaya gerek kalmaz).
            await SignInAsync(await contactChanges.ConfirmAsync(CurrentUserId, kind, code, ct));
            return kind == ContactChangeKind.Email ? "E-posta adresiniz güncellendi." : "Cep telefonunuz güncellendi.";
        }, openForm: false);

    [Authorize, HttpPost("hesabim/iletisim/vazgec")]
    public async Task<IActionResult> CancelContactChange(ContactChangeKind kind, CancellationToken ct)
    {
        await contactChanges.CancelAsync(CurrentUserId, kind, ct);
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>Adımı çalıştırır; hata olursa ilgili form açık ve girilen değerle döner.</summary>
    private async Task<IActionResult> ContactStepAsync(ContactChangeKind kind, Func<Task<string>> step, string? enteredValue = null,
        bool openForm = true)
    {
        try
        {
            TempData["Success"] = await step();
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            TempData["Error"] = ex.Message;
            if (openForm)
            {
                TempData["ContactKind"] = kind.ToString();
                TempData["ContactValue"] = enteredValue;
            }
        }
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
