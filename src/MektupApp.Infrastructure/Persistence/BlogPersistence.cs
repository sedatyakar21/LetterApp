using MektupApp.Domain.Blog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> b)
    {
        b.ToTable("BlogPosts");
        b.HasKey(p => p.Id);
        b.Ignore(p => p.DomainEvents);
        b.Ignore(p => p.IsPublished);
        b.Ignore(p => p.ReadingMinutes);

        b.Property(p => p.Title).HasMaxLength(BlogPost.TitleMax).IsRequired();
        b.Property(p => p.Slug).HasMaxLength(BlogSlug.MaxLength).IsRequired();
        b.HasIndex(p => p.Slug).IsUnique();
        b.Property(p => p.Excerpt).HasMaxLength(BlogPost.ExcerptMax).IsRequired();
        b.Property(p => p.ContentMarkdown).IsRequired(); // nvarchar(max)
        b.Property(p => p.AuthorName).HasMaxLength(100).IsRequired();
        b.Property(p => p.CoverImageKey).HasMaxLength(300);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(p => new { p.Status, p.PublishedAt });
    }
}

internal sealed class BlogPostRepository(AppDbContext db) : IBlogPostRepository
{
    public Task<BlogPost?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<BlogPost?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Set<BlogPost>().FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.Set<BlogPost>().AnyAsync(p => p.Slug == slug && (exceptId == null || p.Id != exceptId), ct);

    public async Task<(IReadOnlyList<BlogPost> Items, int Total)> ListPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.Set<BlogPost>().AsNoTracking().Where(p => p.Status == BlogPostStatus.Published);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(p => p.PublishedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<BlogPost>> ListAllAsync(CancellationToken ct = default) =>
        await db.Set<BlogPost>().AsNoTracking().OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);

    public void Add(BlogPost post) => db.Set<BlogPost>().Add(post);

    public void Remove(BlogPost post) => db.Set<BlogPost>().Remove(post);
}
