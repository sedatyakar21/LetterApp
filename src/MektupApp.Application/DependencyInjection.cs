using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Application.Customers;
using MektupApp.Application.Letters;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Payments;
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
        services.AddScoped<CarrierShippingService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CustomerAdminService>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<CheckoutCalculator>();
        services.AddScoped<PaymentService>();
        services.AddScoped<Blog.BlogService>();
        services.AddScoped<Campaigns.CampaignService>();
        services.AddScoped<Reviews.ReviewService>();
        services.AddScoped<Reviews.BlogCommentService>();

        // Bildirimler: olaylar kuyruğa SMS / e-posta ekler, arka plan servisi gönderir.
        services.AddScoped<Notifications.NotificationTemplates>();
        services.AddScoped<Notifications.NotificationService>();
        services.AddScoped<Notifications.NotificationDispatcher>();
        services.AddScoped<Notifications.NotificationAdminService>();
        services.AddScoped<IDomainEventHandler<MektupApp.Domain.Customers.CustomerRegistered>, Notifications.NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterPaid>, Notifications.NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterShipped>, Notifications.NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterDelivered>, Notifications.NotificationEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterCancelledAfterPayment>, Notifications.NotificationEventHandlers>();

        // MektupPuan: postaya verilen mektupta puan kazanımı, iptalde kullanılan puanın iadesi.
        services.AddScoped<IDomainEventHandler<LetterShipped>, LoyaltyEventHandlers>();
        services.AddScoped<IDomainEventHandler<LetterCancelledAfterPayment>, LoyaltyEventHandlers>();

        // Ödemesi alınmış mektup iptal edilince ödeme sağlayıcısında (PayTR) iade.
        services.AddScoped<IDomainEventHandler<LetterCancelledAfterPayment>, PaymentRefundHandler>();
        return services;
    }
}
