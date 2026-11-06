using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Loyalty;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Payments;
using MektupApp.Infrastructure.Carriers;
using MektupApp.Infrastructure.Payments;
using MektupApp.Infrastructure.Persistence;
using MektupApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MektupApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, string contentRoot)
    {
        services.AddScoped<DomainEventDispatcher>();
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("'ConnectionStrings:Default' yapılandırılmamış.");
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            sql.EnableRetryOnFailure(maxRetryCount: 5);
            // Letter, fotoğraf ve durum geçmişini otomatik yüklediği için JOIN patlamasını önle.
            sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        }));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ILetterRepository, LetterRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IPricingSettingsRepository, PricingSettingsRepository>();
        services.AddScoped<IAdminQueries, AdminQueries>();
        services.AddScoped<ILoyaltyAccountRepository, LoyaltyAccountRepository>();
        services.AddScoped<ILoyaltySettingsRepository, LoyaltySettingsRepository>();

        services.AddSingleton<ISmsSender, LoggingSmsSender>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        var uploads = Path.Combine(contentRoot, config["Storage:UploadsPath"] ?? "App_Data/uploads");
        services.AddSingleton<IFileStorage>(new LocalFileStorage(uploads));

        AddPostalCarrier(services, config);
        AddPaymentProvider(services, config);
        return services;
    }

    /// <summary>
    /// Payment:Provider = "PayTR" ise gerçek PayTR, değilse simülasyon. Süresi dolan ödeme denemeleri
    /// her iki durumda da arka planda kapatılır.
    /// </summary>
    private static void AddPaymentProvider(IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(PaymentOptions.Section);
        services.Configure<PaymentOptions>(section);
        var payment = section.Get<PaymentOptions>() ?? new PaymentOptions();

        services.AddScoped<IPaymentRepository, PaymentRepository>();

        if (payment.UsePayTr)
        {
            var p = payment.PayTR;
            if (string.IsNullOrWhiteSpace(p.MerchantId) || string.IsNullOrWhiteSpace(p.MerchantKey) || string.IsNullOrWhiteSpace(p.MerchantSalt))
                throw new InvalidOperationException(
                    "Payment:Provider = PayTR ama Payment:PayTR:MerchantId / MerchantKey / MerchantSalt tanımlı değil. " +
                    "Bilgileri user-secrets veya ortam değişkeni ile verin (Payment__PayTR__MerchantKey vb.).");

            services.AddHttpClient<IPaymentProvider, PayTrPaymentProvider>(c =>
                c.Timeout = TimeSpan.FromSeconds(Math.Max(5, p.HttpTimeoutSeconds)));
        }
        else
        {
            services.AddSingleton<IPaymentProvider, SimulatedPaymentProvider>();
        }

        services.AddHostedService<PaymentExpiryService>();
    }

    /// <summary>
    /// Ptt:Enabled = true ise gerçek PTT web servisleri, değilse simülasyon taşıyıcısı kullanılır.
    /// Her iki durumda da barkodlar Ptt:Barcode aralığından üretilir ve takip arka planda senkronize edilir.
    /// </summary>
    private static void AddPostalCarrier(IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(PttOptions.Section);
        services.Configure<PttOptions>(section);
        var ptt = section.Get<PttOptions>() ?? new PttOptions();

        if (ptt.Enabled)
        {
            if (ptt.CustomerId <= 0 || string.IsNullOrWhiteSpace(ptt.Password))
                throw new InvalidOperationException(
                    "Ptt:Enabled = true ama Ptt:CustomerId / Ptt:Password tanımlı değil. " +
                    "Bilgileri user-secrets veya ortam değişkeni (Ptt__CustomerId, Ptt__Password) ile verin.");

            services.AddHttpClient<IPostalCarrier, PttPostalCarrier>(c =>
                c.Timeout = TimeSpan.FromSeconds(Math.Max(5, ptt.TimeoutSeconds)));
        }
        else
        {
            services.AddSingleton<IPostalCarrier, SimulatedPostalCarrier>();
        }

        services.AddScoped<IBarcodeAllocator, BarcodeAllocator>();
        services.AddHostedService<CarrierTrackingSyncService>();
    }

    /// <summary>
    /// Bekleyen migration'ları uygular, fiyat listesi yoksa varsayılanlarla oluşturur ve
    /// yapılandırmada tanımlıysa bir yönetici hesabı ekler.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PricingService>().EnsureInitializedAsync();
        await scope.ServiceProvider.GetRequiredService<LoyaltyService>().EnsureInitializedAsync();

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword)) return;

        var email = new Email(adminEmail);
        if (await db.Customers.AnyAsync(c => c.Email == email)) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Customers.Add(Customer.Register("Yönetici", email, new PhoneNumber(config["Seed:AdminPhone"] ?? "5550000000"),
            hasher.Hash(adminPassword), isAdmin: true));
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>()
            .LogInformation("Yönetici hesabı oluşturuldu: {Email}", adminEmail);
    }
}
