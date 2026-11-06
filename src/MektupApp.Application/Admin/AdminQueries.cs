using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Admin;

// Yönetim ekranları için okuma modeli (CQRS-lite).
// Listeleme / raporlama aggregate'leri yüklemeden doğrudan veritabanından projekte edilir;
// durum değiştiren tüm işlemler ise yine aggregate'ler üzerinden yapılır.

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed record LetterSearchCriteria(
    LetterStatus? Status = null,
    string? Query = null,
    Guid? CustomerId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AdminLetterRowDto(
    Guid Id,
    Guid CustomerId,
    string SenderName,
    string RecipientName,
    RecipientType RecipientType,
    string City,
    LetterStatus Status,
    decimal? PaidAmount,
    PaymentMethod? PaymentMethod,
    int PhotoCount,
    string? TrackingCode,
    DateTime? PaidAt,
    DateTime UpdatedAt);

public sealed record DailyStatDto(DateOnly Day, int Orders, decimal Revenue);

public sealed record DashboardDto(
    IReadOnlyDictionary<LetterStatus, int> CountsByStatus,
    decimal RevenueToday,
    decimal RevenueThisMonth,
    decimal RevenueTotal,
    int OrdersToday,
    int CustomerCount,
    int NewCustomersLast7Days,
    IReadOnlyList<DailyStatDto> Last14Days,
    IReadOnlyList<AdminLetterRowDto> RecentOrders,
    IReadOnlyList<ChannelCountDto> Acquisition);

/// <summary>"Bizi nereden duydunuz?" dağılımı. Channel null: bilgi yok (eski / sistem hesapları).</summary>
public sealed record ChannelCountDto(AcquisitionChannel? Channel, int Count, int WithReferral);

public sealed record CustomerSearchCriteria(
    string? Query = null,
    AcquisitionChannel? Channel = null,
    Guid? ReferredBy = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CustomerRowDto(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    bool IsAdmin,
    bool IsActive,
    int OrderCount,
    decimal TotalSpent,
    decimal PointsBalance,
    DateTime CreatedAt,
    AcquisitionChannel? AcquisitionChannel,
    string ReferralCode,
    Guid? ReferredById,
    string? ReferredByName,
    int ReferredCount);

public interface IAdminQueries
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<LetterStatus, int>> CountLettersByStatusAsync(CancellationToken ct = default);
    Task<PagedResult<AdminLetterRowDto>> SearchLettersAsync(LetterSearchCriteria criteria, CancellationToken ct = default);
    Task<PagedResult<CustomerRowDto>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken ct = default);
    Task<CustomerRowDto?> GetCustomerAsync(Guid id, CancellationToken ct = default);
}
