using MektupApp.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.HasKey(p => p.Id);
        b.Ignore(p => p.DomainEvents);

        b.Property(p => p.MerchantOid).HasMaxLength(64).IsRequired();
        b.HasIndex(p => p.MerchantOid).IsUnique();
        b.HasIndex(p => p.LetterId);
        b.HasIndex(p => new { p.Status, p.CreatedAt });

        b.Property(p => p.Provider).HasMaxLength(20);
        b.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.RefundStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.FormUrl).HasMaxLength(500);
        b.Property(p => p.FailureReason).HasMaxLength(300);
        b.Property(p => p.RefundError).HasMaxLength(300);

        foreach (var money in new[] { nameof(Payment.Amount), nameof(Payment.PointsUsed), nameof(Payment.PointsDiscount),
                     nameof(Payment.PointsToEarn), nameof(Payment.ReferralPointsToEarn), nameof(Payment.PaidTotal), nameof(Payment.RefundedAmount),
                     nameof(Payment.CampaignDiscount) })
            b.Property(money).HasPrecision(10, 2);
        b.Property(p => p.BankTransferDiscount).HasPrecision(10, 2);
        b.Property(p => p.CampaignTitle).HasMaxLength(120);
        b.Ignore(p => p.IsBankTransfer);

        // Sağlayıcı bildirimi ile arka plan süre dolumu aynı anda aynı kaydı değiştirmesin.
        b.Property<byte[]>("RowVersion").IsRowVersion();
    }
}

internal sealed class BankTransferSettingsConfiguration : IEntityTypeConfiguration<BankTransferSettings>
{
    public void Configure(EntityTypeBuilder<BankTransferSettings> b)
    {
        b.ToTable("BankTransferSettings");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Ignore(s => s.DomainEvents);
        b.Ignore(s => s.IsAvailable);
        b.Ignore(s => s.IbanDisplay);
        b.Property(s => s.DiscountRate).HasPrecision(5, 4);
        b.Property(s => s.BankName).HasMaxLength(100);
        b.Property(s => s.AccountHolder).HasMaxLength(150);
        b.Property(s => s.Iban).HasMaxLength(34).IsUnicode(false);
        b.Property(s => s.Note).HasMaxLength(500);
        b.Property(s => s.UpdatedBy).HasMaxLength(200);
    }
}

internal sealed class BankTransferSettingsRepository(AppDbContext db) : IBankTransferSettingsRepository
{
    public async Task<BankTransferSettings?> GetAsync(CancellationToken ct = default) =>
        db.Set<BankTransferSettings>().Local.FirstOrDefault(s => s.Id == BankTransferSettings.SingletonId)
        ?? await db.Set<BankTransferSettings>().FirstOrDefaultAsync(s => s.Id == BankTransferSettings.SingletonId, ct);

    public void Add(BankTransferSettings settings) => db.Set<BankTransferSettings>().Add(settings);
}

internal sealed class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByMerchantOidAsync(string merchantOid, CancellationToken ct = default) =>
        db.Set<Payment>().FirstOrDefaultAsync(p => p.MerchantOid == merchantOid, ct);

    public Task<Payment?> GetSucceededForLetterAsync(Guid letterId, CancellationToken ct = default) =>
        db.Set<Payment>()
            .Where(p => p.LetterId == letterId && p.Status == PaymentStatus.Succeeded && p.FailureReason == null)
            .OrderByDescending(p => p.CompletedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListForLetterAsync(Guid letterId, CancellationToken ct = default) =>
        await db.Set<Payment>().AsNoTracking().Where(p => p.LetterId == letterId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListPendingOlderThanAsync(DateTime createdBefore, int maxCount, CancellationToken ct = default) =>
        await db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt < createdBefore && p.Provider != BankTransferSettings.ProviderName)
            .OrderBy(p => p.CreatedAt)
            .Take(maxCount)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListPendingBankTransfersOlderThanAsync(DateTime createdBefore, int maxCount, CancellationToken ct = default) =>
        await db.Set<Payment>()
            .Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt < createdBefore && p.Provider == BankTransferSettings.ProviderName)
            .OrderBy(p => p.CreatedAt)
            .Take(maxCount)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListPendingBankTransfersAsync(CancellationToken ct = default) =>
        await db.Set<Payment>().AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Pending && p.Provider == BankTransferSettings.ProviderName)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public void Add(Payment payment) => db.Set<Payment>().Add(payment);
}
