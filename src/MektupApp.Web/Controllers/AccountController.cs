using System.Security.Claims;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Common;
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

        var customer = await accounts.AuthenticateAsync(model.Email, model.Password, ct);
        if (customer is null)
        {
            ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
            return View(model);
        }

        await SignInAsync(customer);
        return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Letters");
    }

    [HttpGet("kayit")]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost("kayit")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!model.AcceptTerms)
            ModelState.AddModelError(nameof(model.AcceptTerms), "Üyelik sözleşmesini onaylamalısınız.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            var customer = await accounts.RegisterAsync(
                new RegisterCommand(model.FullName, model.Email, model.Phone, model.Password), ct);
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

    [Authorize, HttpPost("cikis")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private Task SignInAsync(AuthenticatedCustomer customer)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.Email, customer.Email)
        };
        if (customer.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
    }
}
