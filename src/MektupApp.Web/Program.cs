using System.Globalization;
using MektupApp.Application;
using MektupApp.Domain.Letters;
using MektupApp.Infrastructure;
using MektupApp.Web.Api;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

var priceList = builder.Configuration.GetSection("Pricing").Get<PriceList>() ?? new PriceList();

builder.Services
    .AddApplication(priceList)
    .AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

var auth = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/giris";
        o.AccessDeniedPath = "/giris";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        // Hesap engellendiyse veya yönetici yetkisi değiştiyse açık oturumu geçersiz say.
        o.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;
    })
    // Google'dan dönen kimlik, üye bulunana / kayıt tamamlanana kadar burada kısa süre tutulur.
    .AddCookie(GoogleSignIn.ExternalScheme, o =>
    {
        o.Cookie.Name = "BiMektup.External";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    })
    // Mobil uygulama: kısa ömürlü erişim token'ı + yenileme token'ı (/api/mobile/auth/refresh).
    .AddBearerToken(MobileApi.Scheme, o =>
    {
        o.BearerTokenExpiration = TimeSpan.FromHours(1);
        o.RefreshTokenExpiration = TimeSpan.FromDays(30);
    });

// Mobil uygulamanın web önizlemesi (flutter run -d web-server) yalnızca geliştirmede, localhost'tan API'yi çağırabilsin.
// Telefon uygulamaları tarayıcı olmadığı için CORS'a takılmaz; canlıda hiçbir kökene izin verilmez.
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddCors(o => o.AddPolicy(MobileApi.CorsPolicy, p => p
    .SetIsOriginAllowed(origin => isDevelopment && Uri.TryCreate(origin, UriKind.Absolute, out var u) && u.IsLoopback)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Google ile giriş: kimlik bilgileri user-secrets / ortam değişkeninden okunur (appsettings.json'a yazmayın).
var googleSection = builder.Configuration.GetSection("Authentication:Google");
var googleConfigured = !string.IsNullOrWhiteSpace(googleSection["ClientId"]) && !string.IsNullOrWhiteSpace(googleSection["ClientSecret"]);
if (googleConfigured)
{
    auth.AddGoogle(GoogleSignIn.Scheme, o =>
    {
        o.SignInScheme = GoogleSignIn.ExternalScheme;
        o.ClientId = googleSection["ClientId"]!;
        o.ClientSecret = googleSection["ClientSecret"]!;
        o.CallbackPath = "/signin-google";
        o.ClaimActions.MapJsonKey("email_verified", "email_verified");
    });
}
builder.Services.AddSingleton(new GoogleSignIn(googleConfigured, Simulation: !googleConfigured && builder.Environment.IsDevelopment()));
builder.Services.AddAuthorization();
builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.Section));

// PayTR ödemede müşterinin gerçek IP'sini (user_ip) ister. Uygulama Nginx / yük dengeleyici arkasındaysa
// X-Forwarded-For okunur; proxy adresini ForwardedHeadersOptions.KnownProxies'e ekleyin.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                         Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<UserFacingExceptionFilter>();
    var formFactory = o.ValueProviderFactories.OfType<Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory>().Single();
    o.ValueProviderFactories[o.ValueProviderFactories.IndexOf(formFactory)] = new InvariantFormValueProviderFactory();
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
// Mobil API'de enum'lar metin olarak gider / gelir ("Card", "Delivered" ...).
.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration);

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var tr = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(tr),
    SupportedCultures = [tr],
    SupportedUICultures = [tr]
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapAreaControllerRoute(
        name: "admin",
        areaName: "Admin",
        pattern: "yonetim/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
