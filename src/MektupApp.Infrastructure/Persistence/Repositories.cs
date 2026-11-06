using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class LetterRepository(AppDbContext db) : ILetterRepository
{
    public Task<Letter?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Letters.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<Letter?> GetByTrackingCodeAsync(string trackingCode, CancellationToken ct = default) =>
        db.Letters.FirstOrDefaultAsync(l => l.TrackingCode == trackingCode, ct);

    public async Task<IReadOnlyList<Letter>> ListByCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        return await db.Letters
            .Where(l => l.CustomerId == customerId && !l.IsDeleted)
            .OrderByDescending(l => l.UpdatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Letter>> ListByStatusAsync(IEnumerable<LetterStatus> statuses, CancellationToken ct = default)
    {
        var set = statuses.ToList();
        return await db.Letters
            .Where(l => set.Contains(l.Status))
            .OrderBy(l => l.UpdatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Letter>> ListForCarrierSyncAsync(string carrier, DateTime checkedBefore, int maxCount, CancellationToken ct = default) =>
        await db.Letters
            .Where(l => l.Status == LetterStatus.Shipped && l.Carrier == carrier &&
                        (l.CarrierCheckedAt == null || l.CarrierCheckedAt < checkedBefore))
            .OrderBy(l => l.CarrierCheckedAt ?? DateTime.MinValue)
            .Take(maxCount)
            .ToListAsync(ct);

    public void Add(Letter letter) => db.Letters.Add(letter);
}

internal sealed class PricingSettingsRepository(AppDbContext db) : IPricingSettingsRepository
{
    public Task<PricingSettings?> GetAsync(CancellationToken ct = default) =>
        db.Set<PricingSettings>().FirstOrDefaultAsync(s => s.Id == PricingSettings.SingletonId, ct);

    public void Add(PricingSettings settings) => db.Set<PricingSettings>().Add(settings);
}

internal sealed class LoyaltyAccountRepository(AppDbContext db) : ILoyaltyAccountRepository
{
    public Task<LoyaltyAccount?> GetByCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        db.Set<LoyaltyAccount>().FirstOrDefaultAsync(a => a.CustomerId == customerId, ct);

    public void Add(LoyaltyAccount account) => db.Set<LoyaltyAccount>().Add(account);
}

internal sealed class LoyaltySettingsRepository(AppDbContext db) : ILoyaltySettingsRepository
{
    public Task<LoyaltySettings?> GetAsync(CancellationToken ct = default) =>
        db.Set<LoyaltySettings>().FirstOrDefaultAsync(s => s.Id == LoyaltySettings.SingletonId, ct);

    public void Add(LoyaltySettings settings) => db.Set<LoyaltySettings>().Add(settings);
}

internal sealed class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        db.Customers.AnyAsync(c => c.Email == email, ct);

    public Task<Customer?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.ReferralCode == referralCode, ct);

    public Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken ct = default) =>
        db.Customers.AnyAsync(c => c.ReferralCode == referralCode, ct);

    public Task<int> CountReferredAsync(Guid referrerId, CancellationToken ct = default) =>
        db.Customers.CountAsync(c => c.ReferredByCustomerId == referrerId, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
}
