using MektupApp.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class CustomerReviewConfiguration : IEntityTypeConfiguration<CustomerReview>
{
    public void Configure(EntityTypeBuilder<CustomerReview> b)
    {
        b.ToTable("CustomerReviews");
        b.HasKey(r => r.Id);
        b.Ignore(r => r.DomainEvents);
        b.Property(r => r.AuthorName).HasMaxLength(100).IsRequired();
        b.Property(r => r.RecipientCity).HasMaxLength(60);
        b.Property(r => r.Comment).HasMaxLength(CustomerReview.CommentMax).IsRequired();
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.RejectionReason).HasMaxLength(300);
        b.HasIndex(r => r.LetterId).IsUnique();               // mektup başına bir yorum
        b.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}

internal sealed class BlogCommentConfiguration : IEntityTypeConfiguration<BlogComment>
{
    public void Configure(EntityTypeBuilder<BlogComment> b)
    {
        b.ToTable("BlogComments");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.DomainEvents);
        b.Property(c => c.AuthorName).HasMaxLength(100).IsRequired();
        b.Property(c => c.Body).HasMaxLength(BlogComment.BodyMax).IsRequired();
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.RejectionReason).HasMaxLength(300);
        b.HasIndex(c => new { c.BlogPostId, c.Status, c.CreatedAt });
        b.HasIndex(c => new { c.Status, c.CreatedAt });
    }
}

internal sealed class CustomerReviewRepository(AppDbContext db) : ICustomerReviewRepository
{
    private DbSet<CustomerReview> Set => db.Set<CustomerReview>();

    public Task<CustomerReview?> GetAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<CustomerReview?> GetByLetterAsync(Guid letterId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(r => r.LetterId == letterId, ct);

    public Task<(IReadOnlyList<CustomerReview> Items, int Total)> ListApprovedAsync(int page, int pageSize, CancellationToken ct = default) =>
        ListByStatusAsync(ModerationStatus.Approved, page, pageSize, ct);

    public async Task<RatingSummary> GetApprovedSummaryAsync(CancellationToken ct = default)
    {
        var counts = await Set.AsNoTracking()
            .Where(r => r.Status == ModerationStatus.Approved)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var total = counts.Sum(c => c.Count);
        var distribution = Enumerable.Range(1, 5).ToDictionary(i => i, i => counts.FirstOrDefault(c => c.Rating == i)?.Count ?? 0);
        var average = total == 0 ? 0 : counts.Sum(c => c.Rating * c.Count) / (double)total;
        return new RatingSummary(average, total, distribution);
    }

    public async Task<(IReadOnlyList<CustomerReview> Items, int Total)> ListByStatusAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().Where(r => status == null || r.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountPendingAsync(CancellationToken ct = default) =>
        Set.CountAsync(r => r.Status == ModerationStatus.Pending, ct);

    public void Add(CustomerReview review) => Set.Add(review);
    public void Remove(CustomerReview review) => Set.Remove(review);
}

internal sealed class BlogCommentRepository(AppDbContext db) : IBlogCommentRepository
{
    private DbSet<BlogComment> Set => db.Set<BlogComment>();

    public Task<BlogComment?> GetAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<BlogComment>> ListApprovedForPostAsync(Guid blogPostId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(c => c.BlogPostId == blogPostId && c.Status == ModerationStatus.Approved)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BlogComment>> ListPendingByCustomerAsync(Guid blogPostId, Guid customerId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(c => c.BlogPostId == blogPostId && c.CustomerId == customerId && c.Status == ModerationStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<BlogComment> Items, int Total)> ListByStatusAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().Where(c => status == null || c.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountPendingAsync(CancellationToken ct = default) =>
        Set.CountAsync(c => c.Status == ModerationStatus.Pending, ct);

    public void Add(BlogComment comment) => Set.Add(comment);
    public void Remove(BlogComment comment) => Set.Remove(comment);
}
