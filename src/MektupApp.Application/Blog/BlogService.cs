using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Domain.Blog;
using MektupApp.Domain.Common;

namespace MektupApp.Application.Blog;

public sealed record BlogPostSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string Excerpt,
    string AuthorName,
    bool HasCover,
    BlogPostStatus Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    int ReadingMinutes);

public sealed record BlogPostDto(
    Guid Id,
    string Title,
    string Slug,
    string Excerpt,
    string ContentMarkdown,
    string AuthorName,
    bool HasCover,
    BlogPostStatus Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    int ReadingMinutes);

/// <param name="Slug">Boşsa başlıktan üretilir.</param>
public sealed record SaveBlogPostCommand(string Title, string? Slug, string Excerpt, string ContentMarkdown, string AuthorName);

/// <summary>Blog use case'leri: ziyaretçiler için yayındaki yazılar, yönetim için yazı yönetimi.</summary>
public sealed class BlogService(IBlogPostRepository posts, IFileStorage files, IUnitOfWork unitOfWork)
{
    private static readonly HashSet<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public const long MaxCoverBytes = 4 * 1024 * 1024;

    // ---------------- Ziyaretçi ----------------

    public async Task<PagedResult<BlogPostSummaryDto>> ListPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await posts.ListPublishedAsync(page, pageSize, ct);
        return new PagedResult<BlogPostSummaryDto>(items.Select(ToSummary).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<BlogPostSummaryDto>> LatestAsync(int count, CancellationToken ct = default) =>
        (await posts.ListPublishedAsync(1, count, ct)).Items.Select(ToSummary).ToList();

    /// <param name="includeDrafts">Yönetici önizlemesi için true.</param>
    public async Task<BlogPostDto?> GetBySlugAsync(string slug, bool includeDrafts, CancellationToken ct = default)
    {
        var post = await posts.GetBySlugAsync(BlogSlug.From(slug), ct);
        return post is null || (!post.IsPublished && !includeDrafts) ? null : ToDto(post);
    }

    public async Task<(Stream Stream, string Key)?> OpenCoverAsync(Guid postId, bool includeDrafts, CancellationToken ct = default)
    {
        var post = await posts.GetAsync(postId, ct);
        if (post?.CoverImageKey is null || (!post.IsPublished && !includeDrafts)) return null;
        var stream = await files.OpenAsync(post.CoverImageKey, ct);
        return stream is null ? null : (stream, post.CoverImageKey);
    }

    // ---------------- Yönetim ----------------

    public async Task<IReadOnlyList<BlogPostSummaryDto>> ListAllAsync(CancellationToken ct = default) =>
        (await posts.ListAllAsync(ct)).Select(ToSummary).ToList();

    public async Task<BlogPostDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await LoadAsync(id, ct));

    public async Task<Guid> CreateAsync(SaveBlogPostCommand cmd, bool publish, CancellationToken ct = default)
    {
        var post = BlogPost.CreateDraft(cmd.Title, cmd.Excerpt, cmd.ContentMarkdown, cmd.AuthorName, cmd.Slug);
        await EnsureUniqueSlugAsync(post, ct);
        if (publish) post.Publish();
        posts.Add(post);
        await unitOfWork.SaveChangesAsync(ct);
        return post.Id;
    }

    public async Task UpdateAsync(Guid id, SaveBlogPostCommand cmd, CancellationToken ct = default)
    {
        var post = await LoadAsync(id, ct);
        post.Edit(cmd.Title, cmd.Excerpt, cmd.ContentMarkdown, cmd.AuthorName);
        if (!string.IsNullOrWhiteSpace(cmd.Slug) && BlogSlug.From(cmd.Slug) != post.Slug)
        {
            post.ChangeSlug(cmd.Slug);
            await EnsureUniqueSlugAsync(post, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task PublishAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, p => p.Publish(), ct);

    public Task UnpublishAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, p => p.Unpublish(), ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var post = await LoadAsync(id, ct);
        var cover = post.CoverImageKey;
        posts.Remove(post);
        await unitOfWork.SaveChangesAsync(ct);
        if (cover is not null) await files.DeleteAsync(cover, ct);
    }

    public async Task SetCoverAsync(Guid id, Stream content, string fileName, long length, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            throw new AppException("Kapak görseli JPG, PNG veya WEBP olmalıdır.");
        if (length is <= 0 or > MaxCoverBytes)
            throw new AppException("Kapak görseli en fazla 4 MB olabilir.");

        var post = await LoadAsync(id, ct);
        var key = await files.SaveAsync(content, ext, ct);
        var old = post.SetCover(key);
        await unitOfWork.SaveChangesAsync(ct);
        if (old is not null) await files.DeleteAsync(old, ct);
    }

    public async Task RemoveCoverAsync(Guid id, CancellationToken ct = default)
    {
        var post = await LoadAsync(id, ct);
        var old = post.SetCover(null);
        await unitOfWork.SaveChangesAsync(ct);
        if (old is not null) await files.DeleteAsync(old, ct);
    }

    // ---------------- yardımcılar ----------------

    /// <summary>Aynı adres başka yazıda varsa sonuna -2, -3 … eklenir.</summary>
    private async Task EnsureUniqueSlugAsync(BlogPost post, CancellationToken ct)
    {
        var baseSlug = post.Slug;
        for (var n = 2; await posts.SlugExistsAsync(post.Slug, post.Id, ct); n++)
        {
            var suffix = $"-{n}";
            var trimmed = baseSlug.Length + suffix.Length > BlogSlug.MaxLength ? baseSlug[..(BlogSlug.MaxLength - suffix.Length)] : baseSlug;
            post.ChangeSlug(trimmed + suffix);
        }
    }

    private async Task ExecuteAsync(Guid id, Action<BlogPost> action, CancellationToken ct)
    {
        action(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<BlogPost> LoadAsync(Guid id, CancellationToken ct) =>
        await posts.GetAsync(id, ct) ?? throw new AppException("Yazı bulunamadı.");

    private static BlogPostSummaryDto ToSummary(BlogPost p) =>
        new(p.Id, p.Title, p.Slug, p.Excerpt, p.AuthorName, p.CoverImageKey is not null, p.Status, p.PublishedAt, p.UpdatedAt, p.ReadingMinutes);

    private static BlogPostDto ToDto(BlogPost p) =>
        new(p.Id, p.Title, p.Slug, p.Excerpt, p.ContentMarkdown, p.AuthorName, p.CoverImageKey is not null, p.Status, p.PublishedAt, p.UpdatedAt, p.ReadingMinutes);
}
