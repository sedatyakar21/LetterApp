using System.Threading.RateLimiting;
using MektupApp.Web.Api;
using Microsoft.AspNetCore.RateLimiting;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Şifremi unuttum uçları için IP başına istek sınırı (15 dakikada 10 istek): SMS/e-posta bombardımanını
/// ve SMS kodunun deneme-yanılmayla bulunmasını zorlaştırır. Üye başına ayrıca saatte 3 talep sınırı vardır.
/// </summary>
public static class PasswordResetRateLimit
{
    public const string Policy = "password-reset";
    private const string Message = "Çok fazla deneme yapıldı. Lütfen 15 dakika sonra tekrar deneyin.";

    public static IServiceCollection AddPasswordResetRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
            o.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                if (http.Request.Path.StartsWithSegments("/api"))
                {
                    await http.Response.WriteAsJsonAsync(new ApiError(Message), ct);
                    return;
                }
                http.Response.ContentType = "text/html; charset=utf-8";
                await http.Response.WriteAsync(
                    $"<!doctype html><html lang=\"tr\"><meta charset=\"utf-8\"><title>Çok fazla deneme</title>" +
                    $"<body style=\"font-family:sans-serif;max-width:480px;margin:3rem auto;padding:0 1rem\"><h1>Çok fazla deneme</h1>" +
                    $"<p>{Message}</p><p><a href=\"/giris\">Giriş sayfasına dön</a></p></body></html>", ct);
            };
        });
}
