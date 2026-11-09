using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> b)
    {
        b.ToTable("Campaigns");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.DomainEvents);

        b.Property(c => c.Title).HasMaxLength(Campaign.TitleMax).IsRequired();
        b.Property(c => c.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        b.HasIndex(c => c.Slug).IsUnique();
        b.Property(c => c.Summary).HasMaxLength(Campaign.SummaryMax).IsRequired();
        b.Property(c => c.DescriptionMarkdown); // nvarchar(max)
        b.Property(c => c.BadgeText).HasMaxLength(Campaign.BadgeMax);
        b.Property(c => c.CouponCode).HasMaxLength(Campaign.CouponMax);
        b.Property(c => c.DiscountType).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.DiscountValue).HasPrecision(10, 2);
        b.Property(c => c.MinOrderAmount).HasPrecision(10, 2);
        b.Property(c => c.Audience).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.CoverImageKey).HasMaxLength(300);
        b.HasIndex(c => new { c.IsPublished, c.StartDate, c.EndDate });
    }
}

internal sealed class CampaignRepository(AppDbContext db) : ICampaignRepository
{
    public Task<Campaign?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Set<Campaign>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Campaign?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Set<Campaign>().FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.Set<Campaign>().AnyAsync(c => c.Slug == slug && (exceptId == null || c.Id != exceptId), ct);

    public async Task<IReadOnlyList<Campaign>> ListCurrentAsync(DateOnly today, CancellationToken ct = default) =>
        await db.Set<Campaign>().AsNoTracking()
            .Where(c => c.IsPublished && (c.EndDate == null || c.EndDate >= today))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Campaign>> ListEndedAsync(DateOnly today, int maxCount, CancellationToken ct = default) =>
        await db.Set<Campaign>().AsNoTracking()
            .Where(c => c.IsPublished && c.EndDate != null && c.EndDate < today)
            .OrderByDescending(c => c.EndDate)
            .Take(maxCount)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Campaign>> ListAllAsync(CancellationToken ct = default) =>
        await db.Set<Campaign>().AsNoTracking().OrderByDescending(c => c.StartDate).ToListAsync(ct);

    public async Task<IReadOnlyList<CampaignUse>> ListUsesAsync(Guid customerId, CancellationToken ct = default)
    {
        // İptal edilmemiş, kampanyalı ödenmiş mektuplar + sonucu beklenen kampanyalı ödeme denemeleri.
        var paid = await db.Letters.AsNoTracking()
            .Where(l => l.CustomerId == customerId && l.CampaignId != null && l.PaidAt != null && l.Status != LetterStatus.Cancelled)
            .Select(l => new { Id = l.CampaignId!.Value, At = l.PaidAt!.Value })
            .ToListAsync(ct);
        var pending = await db.Set<Payment>().AsNoTracking()
            .Where(p => p.CustomerId == customerId && p.CampaignId != null && p.Status == PaymentStatus.Pending)
            .Select(p => new { Id = p.CampaignId!.Value, At = p.CreatedAt })
            .ToListAsync(ct);
        return paid.Concat(pending).Select(x => new CampaignUse(x.Id, DateOnly.FromDateTime(x.At))).ToList();
    }

    public void Add(Campaign campaign) => db.Set<Campaign>().Add(campaign);

    public void Remove(Campaign campaign) => db.Set<Campaign>().Remove(campaign);
}
