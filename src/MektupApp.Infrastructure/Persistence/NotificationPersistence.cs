using MektupApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(n => n.Id);
        b.Ignore(n => n.DomainEvents);
        b.Property(n => n.Channel).HasConversion<string>().HasMaxLength(10);
        b.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        b.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(n => n.Recipient).HasMaxLength(200).IsRequired();
        b.Property(n => n.Subject).HasMaxLength(200);
        b.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        b.Property(n => n.HtmlBody);                          // nvarchar(max)
        b.Property(n => n.LastError).HasMaxLength(500);
        b.Property(n => n.ProviderMessageId).HasMaxLength(100);
        b.HasIndex(n => new { n.Status, n.NextAttemptAt });   // gönderim kuyruğu
        b.HasIndex(n => n.CreatedAt);
        b.HasIndex(n => n.CustomerId);
    }
}

internal sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> b)
    {
        b.ToTable("NotificationSettings");
        b.HasKey(s => s.Id);
        b.Ignore(s => s.DomainEvents);
        b.Property(s => s.SmsTypes).HasConversion<int>();   // bayrak toplamı
        b.Property(s => s.EmailTypes).HasConversion<int>();
    }
}

internal sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    private DbSet<Notification> Set => db.Set<Notification>();

    public Task<Notification?> GetAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> ListDueAsync(DateTime now, int maxCount, CancellationToken ct = default) =>
        await Set.Where(n => n.Status == NotificationStatus.Pending && n.NextAttemptAt <= now)
            .OrderBy(n => n.NextAttemptAt)
            .Take(maxCount)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<Notification> Items, int Total)> SearchAsync(NotificationChannel? channel, NotificationStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking()
            .Where(n => (channel == null || n.Channel == channel) && (status == null || n.Status == status));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<NotificationSettings?> GetSettingsAsync(CancellationToken ct = default) =>
        db.Set<NotificationSettings>().FirstOrDefaultAsync(s => s.Id == NotificationSettings.SingletonId, ct);

    public void Add(Notification notification) => Set.Add(notification);

    public void AddSettings(NotificationSettings settings) => db.Set<NotificationSettings>().Add(settings);
}
