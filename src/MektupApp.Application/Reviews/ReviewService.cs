using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Domain.Blog;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Reviews;

namespace MektupApp.Application.Reviews;

public sealed record ReviewDto(
    Guid Id,
    Guid CustomerId,
    Guid LetterId,
    string AuthorName,
    string? RecipientCity,
    int Rating,
    string Comment,
    ModerationStatus Status,
    string? RejectionReason,
    DateTime CreatedAt);

public sealed record BlogCommentDto(
    Guid Id,
    Guid BlogPostId,
    Guid CustomerId,
    string AuthorName,
    string Body,
    ModerationStatus Status,
    string? RejectionReason,
    DateTime CreatedAt,
    string? PostTitle = null,
    string? PostSlug = null);

/// <summary>Müşteri yorumları: teslim edilen mektup için değerlendirme, onaydan sonra yayın.</summary>
public sealed class ReviewService(
    ICustomerReviewRepository reviews,
    ILetterRepository letters,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork)
{
    // ---------------- Ziyaretçi / üye ----------------

    public async Task<PagedResult<ReviewDto>> ListApprovedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await reviews.ListApprovedAsync(page, pageSize, ct);
        return new PagedResult<ReviewDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public Task<RatingSummary> GetSummaryAsync(CancellationToken ct = default) => reviews.GetApprovedSummaryAsync(ct);

    /// <summary>Mektubun yorumu (varsa). Yalnızca mektubun sahibi görebilir.</summary>
    public async Task<ReviewDto?> GetForLetterAsync(Guid letterId, Guid customerId, CancellationToken ct = default)
    {
        var r = await reviews.GetByLetterAsync(letterId, ct);
        return r is null || r.CustomerId != customerId ? null : ToDto(r);
    }

    public async Task SubmitAsync(Guid customerId, Guid letterId, int rating, string comment, CancellationToken ct = default)
    {
        var letter = await letters.GetAsync(letterId, ct);
        if (letter is null || letter.CustomerId != customerId) throw new AppException("Mektup bulunamadı.");
        if (await reviews.GetByLetterAsync(letterId, ct) is not null)
            throw new AppException("Bu mektup için zaten yorum yazdınız.");
        var customer = await customers.GetAsync(customerId, ct) ?? throw new AppException("Üyelik bulunamadı.");

        reviews.Add(CustomerReview.Write(customerId, letterId, letter.Status == LetterStatus.Delivered, customer.FullName,
            letter.Recipient.Address.City, rating, comment));
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ---------------- Yönetim ----------------

    public async Task<PagedResult<ReviewDto>> ListForModerationAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await reviews.ListByStatusAsync(status, page, pageSize, ct);
        return new PagedResult<ReviewDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public Task<int> CountPendingAsync(CancellationToken ct = default) => reviews.CountPendingAsync(ct);

    public Task ApproveAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, r => r.Approve(), ct);

    public Task RejectAsync(Guid id, string? reason, CancellationToken ct = default) => ExecuteAsync(id, r => r.Reject(reason), ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        reviews.Remove(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ExecuteAsync(Guid id, Action<CustomerReview> action, CancellationToken ct)
    {
        action(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<CustomerReview> LoadAsync(Guid id, CancellationToken ct) =>
        await reviews.GetAsync(id, ct) ?? throw new AppException("Yorum bulunamadı.");

    private static ReviewDto ToDto(CustomerReview r) =>
        new(r.Id, r.CustomerId, r.LetterId, r.AuthorName, r.RecipientCity, r.Rating, r.Comment, r.Status, r.RejectionReason, r.CreatedAt);
}

/// <summary>Blog yorumları: üyeler yazar, yönetici onaylayınca yazının altında görünür.</summary>
public sealed class BlogCommentService(
    IBlogCommentRepository comments,
    IBlogPostRepository posts,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<BlogCommentDto>> ListApprovedAsync(Guid postId, CancellationToken ct = default) =>
        (await comments.ListApprovedForPostAsync(postId, ct)).Select(c => ToDto(c)).ToList();

    /// <summary>Üyenin bu yazıda onay bekleyen yorumları (yalnızca kendisine gösterilir).</summary>
    public async Task<IReadOnlyList<BlogCommentDto>> ListMyPendingAsync(Guid postId, Guid customerId, CancellationToken ct = default) =>
        (await comments.ListPendingByCustomerAsync(postId, customerId, ct)).Select(c => ToDto(c)).ToList();

    public async Task SubmitAsync(Guid postId, Guid customerId, string body, CancellationToken ct = default)
    {
        var post = await posts.GetAsync(postId, ct) ?? throw new AppException("Yazı bulunamadı.");
        var customer = await customers.GetAsync(customerId, ct) ?? throw new AppException("Üyelik bulunamadı.");
        var pending = (await comments.ListPendingByCustomerAsync(postId, customerId, ct)).Count;

        comments.Add(BlogComment.Write(post.Id, post.IsPublished, customerId, customer.FullName, body, pending));
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<BlogCommentDto>> ListForModerationAsync(ModerationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await comments.ListByStatusAsync(status, page, pageSize, ct);
        var titles = new Dictionary<Guid, BlogPost?>();
        foreach (var id in items.Select(c => c.BlogPostId).Distinct())
            titles[id] = await posts.GetAsync(id, ct);
        return new PagedResult<BlogCommentDto>(
            items.Select(c => ToDto(c, titles.GetValueOrDefault(c.BlogPostId))).ToList(), page, pageSize, total);
    }

    public Task<int> CountPendingAsync(CancellationToken ct = default) => comments.CountPendingAsync(ct);

    public Task ApproveAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(id, c => c.Approve(), ct);

    public Task RejectAsync(Guid id, string? reason, CancellationToken ct = default) => ExecuteAsync(id, c => c.Reject(reason), ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        comments.Remove(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ExecuteAsync(Guid id, Action<BlogComment> action, CancellationToken ct)
    {
        action(await LoadAsync(id, ct));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<BlogComment> LoadAsync(Guid id, CancellationToken ct) =>
        await comments.GetAsync(id, ct) ?? throw new AppException("Yorum bulunamadı.");

    private static BlogCommentDto ToDto(BlogComment c, BlogPost? post = null) =>
        new(c.Id, c.BlogPostId, c.CustomerId, c.AuthorName, c.Body, c.Status, c.RejectionReason, c.CreatedAt, post?.Title, post?.Slug);
}
