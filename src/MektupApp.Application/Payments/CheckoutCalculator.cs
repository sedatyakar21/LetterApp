using MektupApp.Application.Abstractions;
using MektupApp.Application.Campaigns;
using MektupApp.Application.Letters;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Application.Payments;

/// <summary>
/// Ödeme ekranındaki tutarı hesaplar: fiyat listesi → kampanya indirimi → puan kullanımı (1 puan = X ₺) → kazanılacak puan.
/// Hem ödeme sayfası (önizleme) hem ödeme başlatma aynı hesabı kullanır.
/// </summary>
public sealed class CheckoutCalculator(
    LetterPricingPolicy pricing,
    PricingService priceLists,
    LoyaltyService loyalty,
    ILoyaltyAccountRepository loyaltyAccounts,
    ICampaignRepository campaigns,
    MektupApp.Domain.Customers.ICustomerRepository customers,
    IPaymentProvider provider)
{
    public async Task<(QuoteDto Quote, LoyaltyAccount? Account)> CalculateAsync(
        Letter letter, PaymentMethod method, bool usePoints, CancellationToken ct, string? couponCode = null)
    {
        var prices = await priceLists.GetCurrentAsync(ct);
        var price = pricing.Calculate(letter, method, prices);
        var account = await loyaltyAccounts.GetByCustomerAsync(letter.CustomerId, ct);
        var balance = account?.Balance ?? 0;
        var settings = await loyalty.GetSettingsAsync(ct);

        // Kampanya: tek kampanya, müşteriye en avantajlı olanı. Mobil ödeme farkı indirim konusu ürüne de eklenir.
        couponCode = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
        var surcharge = method == PaymentMethod.Mobile ? 1 + prices.MobilePaymentSurchargeRate : 1;
        var cart = new CampaignCart(
            price.Total.Amount,
            letter.Scent != Scent.None ? prices.Scent * surcharge : 0,
            letter.Postcard is not null ? prices.Postcard * surcharge : 0);
        var today = TurkeyCalendar.Today;
        var current = await campaigns.ListCurrentAsync(today, ct);
        // Müşteri bilgisi yalnızca hedef kitlesi / kullanım sınırı olan kampanyalar varsa okunur.
        CampaignCustomer? who = null;
        if (current.Any(c => c.HasDiscount && (c.Audience != CampaignAudience.Everyone || c.UsageLimitPerCustomer is not null))
            && await customers.GetAsync(letter.CustomerId, ct) is { } customer)
            who = new CampaignCustomer(customer.MemberSince, customer.BirthDate, await campaigns.ListUsesAsync(customer.Id, ct));
        var applied = CampaignDiscountPolicy.Best(current, cart, couponCode, today, who);
        var campaignDiscount = applied?.Discount ?? 0;
        var afterCampaign = price.Total.Amount - campaignDiscount;

        var redemption = usePoints ? settings.Redeem(afterCampaign, balance) : default;
        var amountToPay = afterCampaign - redemption.Discount;

        var quote = new QuoteDto(
            letter.Id, method,
            price.Lines.Select(l => (l.Description, l.Amount.Amount)).ToList(),
            price.Total.Amount,
            UsePoints: usePoints,
            PointsBalance: balance,
            PointsUsed: redemption.Points,
            AmountToPay: amountToPay,
            PointsToEarn: settings.CalculateReward(amountToPay),
            LoyaltyEnabled: settings.IsEnabled,
            PointsDiscount: redemption.Discount,
            PointValue: settings.PointValue,
            BalanceValue: settings.ToMoney(balance),
            MobilePaymentAvailable: provider.SupportsMobilePayment,
            ProviderLabel: provider.EnvironmentLabel,
            ProviderIsSimulation: provider.IsSimulation)
        {
            Campaign = applied,
            CouponCode = couponCode,
            CouponApplied = applied is not null && current.Any(c => c.Id == applied.CampaignId && c.MatchesCoupon(couponCode)),
            CouponRecognized = current.Any(c => c.HasDiscount && c.PhaseOn(today) == CampaignPhase.Active && c.MatchesCoupon(couponCode)),
            CouponNotEligible = current.Any(c => c.HasDiscount && c.PhaseOn(today) == CampaignPhase.Active && c.MatchesCoupon(couponCode)
                                                 && !c.IsEligible(who, today))
        };
        return (quote, account);
    }
}
