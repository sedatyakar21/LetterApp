using MektupApp.Application.Abstractions;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using Microsoft.EntityFrameworkCore;

namespace MektupApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, DomainEventDispatcher dispatcher)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    /// <summary>
    /// Değişiklikleri kaydeder, ardından aggregate'lerde biriken domain event'lerini yayınlar.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in events)
            await dispatcher.DispatchAsync(domainEvent, cancellationToken);

        return result;
    }
}
