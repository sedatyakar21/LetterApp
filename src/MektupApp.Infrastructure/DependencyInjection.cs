using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Loyalty;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
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
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        var uploads = Path.Combine(contentRoot, config["Storage:UploadsPath"] ?? "App_Data/uploads");
        services.AddSingleton<IFileStorage>(new LocalFileStorage(uploads));
        return services;
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
