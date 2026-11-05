using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Customers;
using MektupApp.Application.Letters;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Letters;
using Microsoft.Extensions.DependencyInjection;

namespace MektupApp.Application;

public static class DependencyInjection
{
    /// <param name="defaultPrices">Veritabanında fiyat kaydı yokken kullanılacak varsayılan fiyatlar.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services, PriceList defaultPrices)
    {
        services.AddSingleton(defaultPrices);
        services.AddSingleton<LetterPricingPolicy>();

        services.AddScoped<PricingService>();
        services.AddScoped<LetterService>();
        services.AddScoped<FulfillmentService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CustomerAdminService>();
        services.AddScoped<LoyaltyService>();

        services.AddScoped<IDomainEventHandler<LetterPaid>, LetterNotificationHandlers>();
        services.AddScoped<IDomainEventHandler<LetterShipped>, LetterNotificationHandlers>();
        services.AddScoped<IDomainEventHandler<LetterDelivered>, LetterNotificationHandlers>();
        services.AddScoped<IDomainEventHandler<LetterCancelledAfterPayment>, LetterNotificationHandlers>();

        // MektupPuan: postaya verilen mektupta puan kazanımı, iptalde kullanılan puanın iadesi.
        services.AddScoped<IDomainEventHandler<LetterShipped>, LoyaltyEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterCancelledAfterPayment>, LoyaltyEventHandlers>();
        return services;
    }
}
