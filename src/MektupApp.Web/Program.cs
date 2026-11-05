using System.Globalization;
using MektupApp.Application;
using MektupApp.Domain.Letters;
using MektupApp.Infrastructure;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

var priceList = builder.Configuration.GetSection("Pricing").Get<PriceList>() ?? new PriceList();

builder.Services
    .AddApplication(priceList)
    .AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/giris";
        o.AccessDeniedPath = "/giris";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        // Hesap engellendiyse veya yönetici yetkisi değiştiyse açık oturumu geçersiz say.
        o.Events.OnValidatePrincipal = SessionValidator.ValidateAsync;
    });
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<UserFacingExceptionFilter>();
    var formFactory = o.ValueProviderFactories.OfType<Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory>().Single();
    o.ValueProviderFactories[o.ValueProviderFactories.IndexOf(formFactory)] = new InvariantFormValueProviderFactory();
    o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration);

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
