using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Web.Infrastructure;
using MektupApp.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MektupApp.Web.Controllers;

/// <summary>
/// Şifremi unuttum: e-posta bağlantısı veya SMS kodu ile yeni şifre belirleme.
/// Yöntemler yönetim panelinden (Bildirimler sayfası) açılıp kapatılır.
/// </summary>
public class PasswordResetController(PasswordResetService resets) : Controller
{
    private const string SentMessage = "Bu e-posta adresiyle kayıtlı bir hesap varsa";

    [HttpGet("sifremi-unuttum")]
    public async Task<IActionResult> Forgot(string? email, CancellationToken ct) =>
        View(await WithOptionsAsync(new ForgotPasswordViewModel { Email = email ?? "" }, ct));

    [HttpPost("sifremi-unuttum"), EnableRateLimiting(PasswordResetRateLimit.Policy)]
    public async Task<IActionResult> Forgot(ForgotPasswordViewModel model, CancellationToken ct)
    {
        await WithOptionsAsync(model, ct);
        if (!ModelState.IsValid) return View(model);
        try
        {
            await resets.RequestAsync(model.Email, model.Method, ct);
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        if (model.Method == PasswordResetMethod.Sms)
        {
            ViewBag.Info = $"{SentMessage}, kayıtlı cep telefonuna {PasswordResetService.CodeLength} haneli bir kod gönderdik. Kod 10 dakika geçerlidir.";
            return View("Code", new ResetPasswordViewModel { Email = model.Email.Trim() });
        }
        ViewBag.Email = model.Email.Trim();
        return View("EmailSent");
    }

    /// <summary>SMS kodu + yeni şifre.</summary>
    [HttpPost("sifremi-unuttum/kod"), EnableRateLimiting(PasswordResetRateLimit.Policy)]
    public async Task<IActionResult> Code(ResetPasswordViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
            ModelState.AddModelError(nameof(model.Code), "Lütfen SMS ile gelen kodu girin.");
        if (string.IsNullOrWhiteSpace(model.Email)) return RedirectToAction(nameof(Forgot));
        if (!ModelState.IsValid) return View(model);
        try
        {
            await resets.ResetWithCodeAsync(model.Email, model.Code, model.Password, ct);
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        return Done();
    }

    /// <summary>E-postadaki bağlantı. Anahtar başka sitelere "Referer" ile sızmasın diye sayfa referrer göndermez.</summary>
    [HttpGet("sifre-sifirla")]
    public async Task<IActionResult> Reset(string? token, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!await resets.IsTokenValidAsync(token, ct)) return View("InvalidLink");
        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost("sifre-sifirla"), EnableRateLimiting(PasswordResetRateLimit.Policy)]
    public async Task<IActionResult> Reset(ResetPasswordViewModel model, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(model);
        try
        {
            await resets.ResetWithTokenAsync(model.Token, model.Password, ct);
        }
        catch (Exception ex) when (ex is AppException or DomainException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        return Done();
    }

    private IActionResult Done()
    {
        TempData["Success"] = "Şifreniz değiştirildi. Yeni şifrenizle giriş yapabilirsiniz. Diğer cihazlardaki oturumlarınız kapatıldı.";
        return RedirectToAction("Login", "Account");
    }

    private async Task<ForgotPasswordViewModel> WithOptionsAsync(ForgotPasswordViewModel model, CancellationToken ct)
    {
        var options = await resets.GetOptionsAsync(ct);
        model.EmailEnabled = options.EmailEnabled;
        model.SmsEnabled = options.SmsEnabled;
        // Seçilen yöntem kapalıysa açık olana geç (yalnızca biri açıksa seçim gösterilmez).
        if (!options.EmailEnabled && options.SmsEnabled) model.Method = PasswordResetMethod.Sms;
        if (options.EmailEnabled && !options.SmsEnabled) model.Method = PasswordResetMethod.Email;
        return model;
    }
}
