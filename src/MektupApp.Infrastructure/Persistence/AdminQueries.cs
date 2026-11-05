using MektupApp.Application.Admin;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace MektupApp.Infrastructure.Persistence;

/// <summary>Yönetim ekranlarının okuma sorguları: aggregate yüklemeden, doğrudan DTO'ya projeksiyon.</summary>
internal sealed class AdminQueries(AppDbContext db) : IAdminQueries
{
    // Ciroya sayılan durumlar (iptal edilenler hariç).
    private static readonly LetterStatus[] RevenueStatuses =
        [LetterStatus.Paid, LetterStatus.InProduction, LetterStatus.Shipped, LetterStatus.Delivered];

    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Müşterinin sildiği taslaklar yönetici için anlamsızdır; diğer her şey görünür.</summary>
    private IQueryable<Letter> AdminLetters =>
        db.Letters.AsNoTracking().IgnoreAutoIncludes()
            .Where(l => !(l.IsDeleted && l.Status == LetterStatus.Draft));

    public async Task<IReadOnlyDictionary<LetterStatus, int>> CountLettersByStatusAsync(CancellationToken ct = default)
    {
        var rows = await AdminLetters
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Enum.GetValues<LetterStatus>()
            .ToDictionary(s => s, s => rows.FirstOrDefault(r => r.Status == s)?.Count ?? 0);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Turkey);
        var todayStartUtc = ToUtc(nowLocal.Date);
        var monthStartUtc = ToUtc(new DateTime(nowLocal.Year, nowLocal.Month, 1));
        var rangeStartUtc = ToUtc(nowLocal.Date.AddDays(-13));

        var revenue = AdminLetters.Where(l => RevenueStatuses.Contains(l.Status) && l.PaidAt != null);

        var counts = await CountLettersByStatusAsync(ct);
        var revenueTotal = await revenue.SumAsync(l => l.PaidAmount ?? 0, ct);
        var revenueMonth = await revenue.Where(l => l.PaidAt >= monthStartUtc).SumAsync(l => l.PaidAmount ?? 0, ct);

        // Son 14 gün: satırlar küçük olduğu için gün bazında gruplama (Türkiye saatine göre) bellekte yapılır.
        var recentPayments = await revenue
            .Where(l => l.PaidAt >= rangeStartUtc)
            .Select(l => new { PaidAt = l.PaidAt!.Value, Amount = l.PaidAmount ?? 0 })
            .ToListAsync(ct);

        var byDay = recentPayments
            .GroupBy(p => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(p.PaidAt, Turkey)))
            .ToDictionary(g => g.Key, g => (Orders: g.Count(), Revenue: g.Sum(x => x.Amount)));

        var days = Enumerable.Range(0, 14)
            .Select(i => DateOnly.FromDateTime(nowLocal.Date.AddDays(i - 13)))
            .Select(d => byDay.TryGetValue(d, out var v)
                ? new DailyStatDto(d, v.Orders, v.Revenue)
                : new DailyStatDto(d, 0, 0))
            .ToList();

        var today = days[^1];
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var recent = await ToRows(AdminLetters.Where(l => l.PaidAt != null).OrderByDescending(l => l.PaidAt))
            .Take(8)
            .ToListAsync(ct);

        return new DashboardDto(
            counts,
            RevenueToday: today.Revenue,
            RevenueThisMonth: revenueMonth,
            RevenueTotal: revenueTotal,
            OrdersToday: today.Orders,
            CustomerCount: await db.Customers.CountAsync(ct),
            NewCustomersLast7Days: await db.Customers.CountAsync(c => c.CreatedAt >= weekAgo, ct),
            Last14Days: days,
            RecentOrders: recent);
    }

    public async Task<PagedResult<AdminLetterRowDto>> SearchLettersAsync(LetterSearchCriteria c, CancellationToken ct = default)
    {
        var query = AdminLetters;

        if (c.Status is { } status)
            query = query.Where(l => l.Status == status);
        if (c.CustomerId is { } customerId)
            query = query.Where(l => l.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(c.Query))
        {
            var q = c.Query.Trim();
            var code = q.ToUpperInvariant().Replace(" ", "");
            query = query.Where(l =>
                l.Recipient.FullName.Contains(q) ||
                l.SenderName.Contains(q) ||
                l.Recipient.Address.City.Contains(q) ||
                l.TrackingCode == code);
        }

        // Operasyon kuyruklarında en eski ödeme önce (ilk giren ilk çıkar); diğerlerinde en yeni önce.
        query = c.Status is LetterStatus.Paid or LetterStatus.InProduction
            ? query.OrderBy(l => l.PaidAt)
            : query.OrderByDescending(l => l.UpdatedAt);

        var (page, size) = Normalize(c.Page, c.PageSize);
        var total = await query.CountAsync(ct);
        var items = await ToRows(query).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AdminLetterRowDto>(items, page, size, total);
    }

    public async Task<PagedResult<CustomerRowDto>> SearchCustomersAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var customers = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            // E-posta ve telefon value object olarak saklandığı için tam eşleşme ile, ad soyad ise içerik ile aranır.
            if (TryCreate(() => new Email(q)) is { } email)
                customers = customers.Where(x => x.Email == email);
            else if (TryCreate(() => new PhoneNumber(q)) is { } phone)
                customers = customers.Where(x => x.Phone == phone);
            else
                customers = customers.Where(x => x.FullName.Contains(q));
        }

        var (p, size) = Normalize(page, pageSize);
        var total = await customers.CountAsync(ct);
        var rows = await ToCustomerRows(customers.OrderByDescending(x => x.CreatedAt).Skip((p - 1) * size).Take(size), ct);
        return new PagedResult<CustomerRowDto>(rows, p, size, total);
    }

    public async Task<CustomerRowDto?> GetCustomerAsync(Guid id, CancellationToken ct = default) =>
        (await ToCustomerRows(db.Customers.AsNoTracking().Where(c => c.Id == id), ct)).FirstOrDefault();

    // ---------- Yardımcılar ----------

    private IQueryable<AdminLetterRowDto> ToRows(IQueryable<Letter> query) =>
        query.Select(l => new AdminLetterRowDto(
            l.Id,
            l.CustomerId,
            l.SenderName,
            l.Recipient.FullName,
            l.Recipient.Type,
            l.Recipient.Address.City,
            l.Status,
            l.PaidAmount,
            l.PaymentMethod,
            db.Set<LetterPhoto>().Count(p => p.LetterId == l.Id),
            l.TrackingCode,
            l.PaidAt,
            l.UpdatedAt));

    private async Task<List<CustomerRowDto>> ToCustomerRows(IQueryable<Customer> query, CancellationToken ct)
    {
        var rows = await query.Select(c => new
        {
            c.Id, c.FullName, c.Email, c.Phone, c.IsAdmin, c.IsActive, c.CreatedAt,
            OrderCount = db.Letters.Count(l => l.CustomerId == c.Id && RevenueStatuses.Contains(l.Status)),
            TotalSpent = db.Letters
                .Where(l => l.CustomerId == c.Id && RevenueStatuses.Contains(l.Status))
                .Sum(l => l.PaidAmount ?? 0),
            Points = db.Set<LoyaltyAccount>()
                .Where(a => a.CustomerId == c.Id)
                .Select(a => (decimal?)a.Balance)
                .FirstOrDefault() ?? 0
        }).ToListAsync(ct);

        return rows.Select(r => new CustomerRowDto(
                r.Id, r.FullName, r.Email.Value, r.Phone.ToString(), r.IsAdmin, r.IsActive,
                r.OrderCount, r.TotalSpent, r.Points, r.CreatedAt))
            .ToList();
    }

    private static (int Page, int Size) Normalize(int page, int size) =>
        (Math.Max(1, page), Math.Clamp(size, 5, 100));

    private static DateTime ToUtc(DateTime turkeyLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(turkeyLocal, DateTimeKind.Unspecified), Turkey);

    private static T? TryCreate<T>(Func<T> factory) where T : class
    {
        try { return factory(); }
        catch (DomainException) { return null; }
    }
}
