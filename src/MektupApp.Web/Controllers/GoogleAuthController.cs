using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Controllers;

public sealed class GoogleRegisterViewModel
{
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100), Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cep telefonu zorunludur."), Display(Name = "Cep Telefonu")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Bizi nereden duydunuz?")]
    public AcquisitionChannel? Channel { get; set; }

    [StringLength(20), Display(Name = "Arkadaşınızın referans no'su")]
    public string? ReferralCode { get; set; }

    [Display(Name = "Doğum tarihi")]
    public DateOnly? BirthDate { get; set; }

    public bool AcceptTerms { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class GoogleSimulationViewModel
{
    [Required, StringLength(100)] public string FullName { get; set; } = "Deniz Test";
    [Required, EmailAddress] public string Email { get; set; } = "deniz.google@example.com";
    public bool EmailVerified { get; set; } = true;
    public string? ReturnUrl { get; set; }
    public string? Mode { get; set; }
}

/// <summary>
/// Google ile giriş / kayıt / hesap bağlama. Akış:
/// 1) POST /giris/google → Google'a yönlendir (veya geliştirmede simülasyon sayfası).
/// 2) Google, kimliği "External" çereze yazar ve /giris/google/donus adresine döner.
/// 3) Üye bulunursa giriş yapılır; bulunmazsa /kayit/google formunda telefon vb. istenir.
/// </summary>
public class GoogleAuthController(AccountService accounts, GoogleSignIn google, IWebHostEnvironment env) : Controller
{
    [HttpPost("giris/google")]
    public IActionResult Start(string? returnUrl, bool baglan = false)
    {
        if (!google.Available) return NotFound();
        if (baglan && User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Account");

        var mode = baglan ? GoogleSignIn.LinkMode : "login";
        returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (google.Simulation)
            return RedirectToAction(nameof(Simulation), new { returnUrl, mode });

        var props = new AuthenticationProperties { RedirectUri = Url.Action(nameof(Callback), new { returnUrl }) };
        props.Items[GoogleSignIn.ModeKey] = mode;
        return Challenge(props, GoogleSignIn.Scheme);
    }

    [HttpGet("giris/google/donus")]
    public async Task<IActionResult> Callback(string? returnUrl, CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync(GoogleSignIn.ExternalScheme);
        var identity = result.Succeeded ? GoogleSignIn.Read(result.Principal) : null;
        if (identity is null) return Fail("Google ile giriş tamamlanamadı. Lütfen tekrar deneyin.");

        try
        {
            if (result.Properties?.Items.TryGetValue(GoogleSignIn.ModeKey, out var mode) == true && mode == GoogleSignIn.LinkMode)
            {
                if (User.Identity?.IsAuthenticated != true) return Fail("Google'ı bağlamak için önce giriş yapın.");
                await accounts.LinkGoogleAsync(User.GetCustomerId(), identity, ct);
                await HttpContext.SignOutAsync(GoogleSignIn.ExternalScheme);
                TempData["Success"] = $"Google hesabınız ({identity.Email}) bağlandı. Artık Google ile giriş yapabilirsiniz.";
                return RedirectToAction("Profile", "Account");
            }

            var customer = await accounts.SignInWithGoogleAsync(identity, ct);
            if (customer is null)
                return RedirectToAction(nameof(Register), new { returnUrl }); // "External" çerez kayıt bitene kadar kalır

            await HttpContext.SignOutAsync(GoogleSignIn.ExternalScheme);
            await HttpContext.SignInCustomerAsync(customer);
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Letters");
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            return await FailAndForgetAsync(ex.Message);
        }
    }

    [HttpGet("kayit/google")]
    public async Task<IActionResult> Register(string? returnUrl)
    {
        var identity = await ReadExternalAsync();
        if (identity is null) return Fail("Google oturumunun süresi doldu. Lütfen tekrar \"Google ile devam et\" deyin.");
        return View(new GoogleRegisterViewModel { Email = identity.Email, FullName = identity.FullName, ReturnUrl = returnUrl });
    }

    [HttpPost("kayit/google")]
    public async Task<IActionResult> Register(GoogleRegisterViewModel model, CancellationToken ct)
    {
        var identity = await ReadExternalAsync();
        if (identity is null) return Fail("Google oturumunun süresi doldu. Lütfen tekrar \"Google ile devam et\" deyin.");
        model.Email = identity.Email; // e-posta Google'dan gelir, formdan değiştirilemez

        if (!model.AcceptTerms)
            ModelState.AddModelError(nameof(model.AcceptTerms), "Üyelik sözleşmesini onaylamalısınız.");
        if (model.Channel is null)
            ModelState.AddModelError(nameof(model.Channel), "Lütfen bizi nereden duyduğunuzu seçin.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            var customer = await accounts.RegisterWithGoogleAsync(new GoogleRegisterCommand(identity, model.FullName, model.Phone,
                model.Channel, model.Channel == AcquisitionChannel.Friend ? model.ReferralCode : null, model.BirthDate), ct);
            await HttpContext.SignOutAsync(GoogleSignIn.ExternalScheme);
            await HttpContext.SignInCustomerAsync(customer);
            TempData["Success"] = "Hoş geldiniz! Google hesabınızla üyeliğiniz oluşturuldu.";
            return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Write", "Letters");
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    // ---------------- Geliştirme simülasyonu (Google anahtarı yokken) ----------------

    [HttpGet("giris/google/simulasyon")]
    public IActionResult Simulation(string? returnUrl, string? mode)
    {
        if (!google.Simulation || !env.IsDevelopment()) return NotFound();
        return View(new GoogleSimulationViewModel { ReturnUrl = returnUrl, Mode = mode });
    }

    [HttpPost("giris/google/simulasyon")]
    public async Task<IActionResult> Simulation(GoogleSimulationViewModel model)
    {
        if (!google.Simulation || !env.IsDevelopment()) return NotFound();
        if (!ModelState.IsValid) return View(model);

        // Gerçek Google'daki gibi kalıcı bir "sub" üret: aynı e-posta hep aynı simüle hesabı temsil eder.
        var sub = "sim-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(model.Email.Trim().ToLowerInvariant())))[..20].ToLowerInvariant();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim(ClaimTypes.Email, model.Email.Trim()),
            new Claim(ClaimTypes.Name, model.FullName.Trim()),
            new Claim("email_verified", model.EmailVerified.ToString())
        ], GoogleSignIn.Scheme));

        var props = new AuthenticationProperties();
        props.Items[GoogleSignIn.ModeKey] = model.Mode == GoogleSignIn.LinkMode ? GoogleSignIn.LinkMode : "login";
        await HttpContext.SignInAsync(GoogleSignIn.ExternalScheme, principal, props);
        return RedirectToAction(nameof(Callback), new { returnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null });
    }

    // ---------------- yardımcılar ----------------

    private async Task<GoogleIdentity?> ReadExternalAsync()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleSignIn.ExternalScheme);
        return result.Succeeded ? GoogleSignIn.Read(result.Principal) : null;
    }

    private IActionResult Fail(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction("Login", "Account");
    }

    private async Task<IActionResult> FailAndForgetAsync(string message)
    {
        await HttpContext.SignOutAsync(GoogleSignIn.ExternalScheme);
        return User.Identity?.IsAuthenticated == true
            ? RedirectWithError(message, Url.Action("Profile", "Account")!)
            : Fail(message);
    }

    private IActionResult RedirectWithError(string message, string url)
    {
        TempData["Error"] = message;
        return Redirect(url);
    }
}
