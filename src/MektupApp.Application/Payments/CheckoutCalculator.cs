using MektupApp.Application.Abstractions;
using MektupApp.Application.Letters;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Application.Payments;

/// <summary>
/// Ödeme ekranındaki tutarı hesaplar: fiyat listesi + puan kullanımı (1 puan = X ₺) + kazanılacak puan.
/// Hem ödeme sayfası (önizleme) hem ödeme başlatma aynı hesabı kullanır.
/// </summary>
public sealed class CheckoutCalculator(
    LetterPricingPolicy pricing,
    PricingService priceLists,
    LoyaltyService loyalty,
    ILoyaltyAccountRepository loyaltyAccounts,
    IPaymentProvider provider)
{
    public async Task<(QuoteDto Quote, LoyaltyAccount? Account)> CalculateAsync(
        Letter letter, PaymentMethod method, bool usePoints, CancellationToken ct)
    {
        var price = pricing.Calculate(letter, method, await priceLists.GetCurrentAsync(ct));
        var account = await loyaltyAccounts.GetByCustomerAsync(letter.CustomerId, ct);
        var balance = account?.Balance ?? 0;
        var settings = await loyalty.GetSettingsAsync(ct);

        var redemption = usePoints ? settings.Redeem(price.Total.Amount, balance) : default;
        var amountToPay = price.Total.Amount - redemption.Discount;

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
            ProviderIsSimulation: provider.IsSimulation);
        return (quote, account);
    }
}
