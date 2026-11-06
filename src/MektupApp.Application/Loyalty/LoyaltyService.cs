using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Application.Loyalty;

public sealed record LoyaltySettingsDto(
    bool IsEnabled, RewardType RewardType, decimal Value,
    bool ReferralEnabled, RewardType ReferralRewardType, decimal ReferralValue,
    decimal PointValue,
    DateTime UpdatedAt, string? UpdatedBy);

public sealed record LoyaltySettingsInput(
    bool IsEnabled, RewardType RewardType, decimal Value,
    bool ReferralEnabled, RewardType ReferralRewardType, decimal ReferralValue,
    decimal PointValue);

public sealed record PointTransactionDto(PointTransactionType Type, decimal Points, Guid? LetterId, string Note, DateTime At);

public sealed record LoyaltyAccountDto(decimal Balance, decimal TotalEarned, IReadOnlyList<PointTransactionDto> Transactions);

/// <summary>MektupPuan use case'leri: ayarlar, bakiye / hareketler ve yönetici düzeltmesi.</summary>
public sealed class LoyaltyService(
    ILoyaltySettingsRepository settingsRepo,
    ILoyaltyAccountRepository accounts,
    IUnitOfWork unitOfWork)
{
    public async Task<LoyaltySettings> GetSettingsAsync(CancellationToken ct = default) =>
        await settingsRepo.GetAsync(ct) ?? LoyaltySettings.CreateDefault();

    public async Task<LoyaltySettingsDto> GetSettingsInfoAsync(CancellationToken ct = default)
    {
        var s = await GetSettingsAsync(ct);
        return new LoyaltySettingsDto(s.IsEnabled, s.RewardType, s.Value,
            s.ReferralEnabled, s.ReferralRewardType, s.ReferralValue, s.PointValue, s.UpdatedAt, s.UpdatedBy);
    }

    public async Task UpdateSettingsAsync(LoyaltySettingsInput input, string updatedBy, CancellationToken ct = default)
    {
        var s = await settingsRepo.GetAsync(ct);
        if (s is null)
        {
            s = LoyaltySettings.CreateDefault();
            settingsRepo.Add(s);
        }
        s.Update(input.IsEnabled, input.RewardType, input.Value, updatedBy);
        s.UpdateReferral(input.ReferralEnabled, input.ReferralRewardType, input.ReferralValue, updatedBy);
        s.UpdatePointValue(input.PointValue, updatedBy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (await settingsRepo.GetAsync(ct) is not null) return;
        settingsRepo.Add(LoyaltySettings.CreateDefault());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken ct = default) =>
        (await accounts.GetByCustomerAsync(customerId, ct))?.Balance ?? 0;

    public async Task<LoyaltyAccountDto> GetAccountAsync(Guid customerId, CancellationToken ct = default)
    {
        var acc = await accounts.GetByCustomerAsync(customerId, ct);
        if (acc is null) return new LoyaltyAccountDto(0, 0, []);
        return new LoyaltyAccountDto(acc.Balance, acc.TotalEarned,
            acc.Transactions.OrderByDescending(t => t.At)
                .Select(t => new PointTransactionDto(t.Type, t.Points, t.LetterId, t.Note, t.At))
                .ToList());
    }

    public async Task AdjustAsync(Guid customerId, decimal points, string reason, string adjustedBy, CancellationToken ct = default)
    {
        var acc = await GetOrOpenAccountAsync(customerId, ct);
        acc.Adjust(points, $"{reason} ({adjustedBy})");
        await unitOfWork.SaveChangesAsync(ct);
    }

    internal async Task<LoyaltyAccount> GetOrOpenAccountAsync(Guid customerId, CancellationToken ct)
    {
        var acc = await accounts.GetByCustomerAsync(customerId, ct);
        if (acc is not null) return acc;
        acc = LoyaltyAccount.Open(customerId);
        accounts.Add(acc);
        return acc;
    }
}

/// <summary>
/// Mektup olaylarına tepki olarak puan hareketi oluşturur:
/// postaya verilen mektup → puan kazanılır; ödeme sonrası iptal → kullanılan puan iade edilir.
/// </summary>
public sealed class LoyaltyEventHandlers(
    LoyaltyService loyalty,
    ICustomerRepository customers,
    ISmsSender sms,
    IUnitOfWork unitOfWork) :
    IDomainEventHandler<LetterShipped>,
    IDomainEventHandler<LetterCancelledAfterPayment>
{
    public async Task HandleAsync(LetterShipped e, CancellationToken ct)
    {
        await CreditSenderAsync(e, ct);
        await CreditReferrerAsync(e, ct);
    }

    /// <summary>Mektubu gönderen üyenin kendi kazancı.</summary>
    private async Task CreditSenderAsync(LetterShipped e, CancellationToken ct)
    {
        if (e.PointsToEarn <= 0) return;

        var account = await loyalty.GetOrOpenAccountAsync(e.CustomerId, ct);
        if (account.Earn(e.LetterId, e.PointsToEarn) is null) return;
        await unitOfWork.SaveChangesAsync(ct);

        var customer = await customers.GetAsync(e.CustomerId, ct);
        if (customer is not null)
            await sms.SendAsync(customer.Phone.Value,
                $"Tebrikler! Mektubunuz icin {e.PointsToEarn:0.00} MektupPuan kazandiniz. Bakiye: {account.Balance:0.00}", ct);
    }

    /// <summary>Gönderen üyeyi davet eden üyenin referans kazancı.</summary>
    private async Task CreditReferrerAsync(LetterShipped e, CancellationToken ct)
    {
        if (e.ReferrerCustomerId is not { } referrerId || e.ReferralPointsToEarn <= 0) return;

        var referrer = await customers.GetAsync(referrerId, ct);
        if (referrer is null || !referrer.IsActive) return;

        var sender = await customers.GetAsync(e.CustomerId, ct);
        var account = await loyalty.GetOrOpenAccountAsync(referrerId, ct);
        if (account.EarnReferral(e.LetterId, e.ReferralPointsToEarn, MaskName(sender?.FullName)) is null) return;
        await unitOfWork.SaveChangesAsync(ct);

        await sms.SendAsync(referrer.Phone.Value,
            $"Davet ettiginiz arkadasiniz mektup gonderdi; {e.ReferralPointsToEarn:0.00} MektupPuan kazandiniz. Bakiye: {account.Balance:0.00}", ct);
    }

    /// <summary>"Ayşe Yılmaz" → "Ayşe Y." (davet eden üyeye tam ad gösterilmez).</summary>
    private static string MaskName(string? fullName)
    {
        var parts = (fullName ?? "Bir arkadaşınız").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 1 ? parts.FirstOrDefault() ?? "Bir arkadaşınız" : $"{parts[0]} {parts[^1][0]}.";
    }

    public async Task HandleAsync(LetterCancelledAfterPayment e, CancellationToken ct)
    {
        if (e.PointsUsed <= 0) return;

        var account = await loyalty.GetOrOpenAccountAsync(e.CustomerId, ct);
        if (account.RefundSpent(e.LetterId) is not null)
            await unitOfWork.SaveChangesAsync(ct);
    }
}
