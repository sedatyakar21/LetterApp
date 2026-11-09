using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Reviews;

/// <summary>Kullanıcı içeriğinin yayın durumu: yönetici onaylamadan sitede görünmez.</summary>
public enum ModerationStatus
{
    [Display(Name = "Onay bekliyor")] Pending = 0,
    [Display(Name = "Yayında")] Approved = 1,
    [Display(Name = "Reddedildi")] Rejected = 2
}

/// <summary>Sitede gösterilen yazar adı: gizlilik için soyadın yalnızca baş harfi ("Ayşe Yılmaz" → "Ayşe Y.").</summary>
public static class PublicName
{
    public static string From(string fullName)
    {
        var parts = (fullName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Üye",
            1 => parts[0],
            _ => $"{string.Join(' ', parts[..^1])} {char.ToUpper(parts[^1][0], new System.Globalization.CultureInfo("tr-TR"))}."
        };
    }
}

/// <summary>Onay akışı ortak kuralları.</summary>
public abstract class ModeratedContent : AggregateRoot
{
    public ModerationStatus Status { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? ModeratedAt { get; protected set; }
    /// <summary>Reddetme nedeni (yalnızca yöneticiler ve yazan üye görür).</summary>
    public string? RejectionReason { get; protected set; }

    public bool IsApproved => Status == ModerationStatus.Approved;

    public void Approve()
    {
        Status = ModerationStatus.Approved;
        RejectionReason = null;
        ModeratedAt = DateTime.UtcNow;
    }

    public void Reject(string? reason)
    {
        Status = ModerationStatus.Rejected;
        RejectionReason = Guard.Optional(reason, "Ret nedeni", 300);
        ModeratedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Müşteri yorumu (aggregate root): teslim edilen bir mektup için hizmet değerlendirmesi.
/// Her mektup için en fazla bir yorum yazılır; yönetici onaylayınca yayınlanır.
/// </summary>
public sealed class CustomerReview : ModeratedContent
{
    public const int CommentMin = 10;
    public const int CommentMax = 1000;

    private CustomerReview() { } // EF Core

    public Guid CustomerId { get; private set; }
    public Guid LetterId { get; private set; }
    /// <summary>Sitede görünen ad (yorum yazıldığı andaki üyelik adından, ör. "Ayşe Y.").</summary>
    public string AuthorName { get; private set; } = string.Empty;
    /// <summary>Mektubun gittiği şehir (bağlam için: "İstanbul'a mektup gönderdi").</summary>
    public string? RecipientCity { get; private set; }
    public int Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;

    /// <param name="letterDelivered">Mektup teslim edildi mi; yorum yalnızca teslimden sonra yazılabilir.</param>
    public static CustomerReview Write(Guid customerId, Guid letterId, bool letterDelivered, string customerFullName,
        string? recipientCity, int rating, string comment)
    {
        if (!letterDelivered)
            throw new DomainException("Yorum, mektubunuz teslim edildikten sonra yazılabilir.");
        if (rating is < 1 or > 5)
            throw new DomainException("Lütfen 1 ile 5 arasında puan verin.");

        var text = Guard.Required(comment, "Yorum", CommentMax);
        if (text.Length < CommentMin)
            throw new DomainException($"Yorumunuz en az {CommentMin} karakter olmalıdır.");

        return new CustomerReview
        {
            CustomerId = customerId,
            LetterId = letterId,
            AuthorName = PublicName.From(customerFullName),
            RecipientCity = Guard.Optional(recipientCity, "Şehir", 60),
            Rating = rating,
            Comment = text,
            Status = ModerationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>Blog yazısına üye yorumu (aggregate root). Yönetici onaylayınca yazının altında görünür.</summary>
public sealed class BlogComment : ModeratedContent
{
    public const int BodyMin = 2;
    public const int BodyMax = 1000;
    /// <summary>Bir üyenin aynı yazıda aynı anda onay bekleyebilecek en fazla yorum sayısı (spam önlemi).</summary>
    public const int MaxPendingPerPost = 3;

    private BlogComment() { } // EF Core

    public Guid BlogPostId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    public static BlogComment Write(Guid blogPostId, bool postIsPublished, Guid customerId, string customerFullName,
        string body, int pendingByCustomerOnPost)
    {
        if (!postIsPublished) throw new DomainException("Bu yazıya yorum yapılamaz.");
        if (pendingByCustomerOnPost >= MaxPendingPerPost)
            throw new DomainException("Önceki yorumlarınız onay bekliyor. Lütfen onaylandıktan sonra tekrar deneyin.");

        var text = Guard.Required(body, "Yorum", BodyMax);
        if (text.Length < BodyMin) throw new DomainException("Yorum çok kısa.");

        return new BlogComment
        {
            BlogPostId = blogPostId,
            CustomerId = customerId,
            AuthorName = PublicName.From(customerFullName),
            Body = text,
            Status = ModerationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public sealed record RatingSummary(double Average, int Count, IReadOnlyDictionary<int, int> Distribution);

public interface ICustomerReviewRepository
{
    Task<CustomerReview?> GetAsync(Guid id, CancellationToken ct = default);
    Task<CustomerReview?> GetByLetterAsync(Guid letterId, CancellationToken ct = default);
    Task<(IReadOnlyList<CustomerReview> Items, int Total)> ListApprovedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<RatingSummary> GetApprovedSummaryAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<CustomerReview> Items, int Total)> ListByStatusAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountPendingAsync(CancellationToken ct = default);
    void Add(CustomerReview review);
    void Remove(CustomerReview review);
}

public interface IBlogCommentRepository
{
    Task<BlogComment?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BlogComment>> ListApprovedForPostAsync(Guid blogPostId, CancellationToken ct = default);
    Task<IReadOnlyList<BlogComment>> ListPendingByCustomerAsync(Guid blogPostId, Guid customerId, CancellationToken ct = default);
    Task<(IReadOnlyList<BlogComment> Items, int Total)> ListByStatusAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountPendingAsync(CancellationToken ct = default);
    void Add(BlogComment comment);
    void Remove(BlogComment comment);
}
