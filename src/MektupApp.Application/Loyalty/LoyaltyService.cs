using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Application.Loyalty;

public sealed record LoyaltySettingsDto(bool IsEnabled, RewardType RewardType, decimal Value, DateTime UpdatedAt, string? UpdatedBy);

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
        return new LoyaltySettingsDto(s.IsEnabled, s.RewardType, s.Value, s.UpdatedAt, s.UpdatedBy);
    }

    public async Task UpdateSettingsAsync(bool isEnabled, RewardType type, decimal value, string updatedBy, CancellationToken ct = default)
    {
        var s = await settingsRepo.GetAsync(ct);
        if (s is null)
        {
            s = LoyaltySettings.CreateDefault();
            settingsRepo.Add(s);
        }
        s.Update(isEnabled, type, value, updatedBy);
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
        if (e.PointsToEarn <= 0) return;

        var account = await loyalty.GetOrOpenAccountAsync(e.CustomerId, ct);
        if (account.Earn(e.LetterId, e.PointsToEarn) is null) return;
        await unitOfWork.SaveChangesAsync(ct);

        var customer = await customers.GetAsync(e.CustomerId, ct);
        if (customer is not null)
            await sms.SendAsync(customer.Phone.Value,
                $"Tebrikler! Mektubunuz icin {e.PointsToEarn:0.00} MektupPuan kazandiniz. Bakiye: {account.Balance:0.00}", ct);
    }

    public async Task HandleAsync(LetterCancelledAfterPayment e, CancellationToken ct)
    {
        if (e.PointsUsed <= 0) return;

        var account = await loyalty.GetOrOpenAccountAsync(e.CustomerId, ct);
        if (account.RefundSpent(e.LetterId) is not null)
            await unitOfWork.SaveChangesAsync(ct);
    }
}
