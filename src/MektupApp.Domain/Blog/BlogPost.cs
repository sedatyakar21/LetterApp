using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Blog;

public enum BlogPostStatus
{
    [Display(Name = "Taslak")] Draft = 0,
    [Display(Name = "Yayında")] Published = 1
}

/// <summary>
/// Blog yazısı (aggregate root). İçerik Markdown olarak saklanır; HTML'e çevirme sunum katmanındadır.
/// Yayındaki yazının adresi (slug) değiştirilebilir ama boş / geçersiz olamaz.
/// </summary>
public sealed class BlogPost : AggregateRoot
{
    public const int TitleMax = 150;
    public const int ExcerptMax = 300;
    public const int ContentMax = 100_000;
    private const int WordsPerMinute = 200;

    private BlogPost() { } // EF Core

    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    /// <summary>Liste kartında ve arama motoru açıklamasında (meta description) görünen kısa özet.</summary>
    public string Excerpt { get; private set; } = string.Empty;
    public string ContentMarkdown { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string? CoverImageKey { get; private set; }

    public BlogPostStatus Status { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public bool IsPublished => Status == BlogPostStatus.Published;

    /// <summary>Yaklaşık okuma süresi (dakika, en az 1).</summary>
    public int ReadingMinutes => Math.Max(1, (int)Math.Ceiling(
        ContentMarkdown.Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length / (double)WordsPerMinute));

    public static BlogPost CreateDraft(string title, string excerpt, string contentMarkdown, string authorName, string? slug = null)
    {
        var now = DateTime.UtcNow;
        var post = new BlogPost { Status = BlogPostStatus.Draft, CreatedAt = now };
        post.Edit(title, excerpt, contentMarkdown, authorName);
        post.ChangeSlug(string.IsNullOrWhiteSpace(slug) ? title : slug);
        return post;
    }

    public void Edit(string title, string excerpt, string contentMarkdown, string authorName)
    {
        Title = Guard.Required(title, "Başlık", TitleMax);
        Excerpt = Guard.Required(excerpt, "Özet", ExcerptMax);
        ContentMarkdown = Guard.Required(contentMarkdown, "İçerik", ContentMax);
        AuthorName = Guard.Required(authorName, "Yazar", 100);
        Touch();
    }

    /// <summary>Adresi verilen metinden üretir ("Askere Mektup Nasıl Gönderilir?" → "askere-mektup-nasil-gonderilir").</summary>
    public void ChangeSlug(string slugOrTitle)
    {
        var slug = BlogSlug.From(slugOrTitle);
        if (slug.Length == 0) throw new DomainException("Yazı adresi en az bir harf veya rakam içermelidir.");
        Slug = slug;
        Touch();
    }

    public void Publish()
    {
        if (IsPublished) return;
        Status = BlogPostStatus.Published;
        PublishedAt ??= DateTime.UtcNow; // tekrar yayınlanınca ilk yayın tarihi korunur
        Touch();
    }

    public void Unpublish()
    {
        Status = BlogPostStatus.Draft;
        Touch();
    }

    /// <returns>Varsa eski kapak dosyasının anahtarı (silinmesi için).</returns>
    public string? SetCover(string? imageKey)
    {
        var old = CoverImageKey;
        CoverImageKey = Guard.Optional(imageKey, "Kapak görseli", 300);
        Touch();
        return old == CoverImageKey ? null : old;
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>URL'de kullanılacak yazı adresi: küçük harf, Türkçe karakterler sadeleşmiş, kelimeler tireyle ayrılmış.</summary>
public static partial class BlogSlug
{
    public const int MaxLength = 120;

    public static string From(string? text)
    {
        var sb = new StringBuilder();
        foreach (var ch in (text ?? string.Empty).Trim())
        {
            sb.Append(ch switch
            {
                'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'I' or 'İ' or 'i' => 'i',
                'ö' or 'Ö' => 'o', 'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(ch)
            });
        }
        var slug = NonAlnum().Replace(sb.ToString(), "-").Trim('-');
        if (slug.Length > MaxLength) slug = slug[..MaxLength].TrimEnd('-');
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();
}

public interface IBlogPostRepository
{
    Task<BlogPost?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BlogPost?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default);
    Task<(IReadOnlyList<BlogPost> Items, int Total)> ListPublishedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<BlogPost>> ListAllAsync(CancellationToken ct = default);
    void Add(BlogPost post);
    void Remove(BlogPost post);
}
