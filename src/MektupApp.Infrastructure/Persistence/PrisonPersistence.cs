using MektupApp.Domain.Prisons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class PrisonConfiguration : IEntityTypeConfiguration<Prison>
{
    public void Configure(EntityTypeBuilder<Prison> b)
    {
        b.ToTable("Prisons");
        b.HasKey(p => p.Id);
        b.Ignore(p => p.DomainEvents);
        b.Ignore(p => p.HasFullAddress);
        b.Property(p => p.Name).HasMaxLength(Prison.NameMax).IsRequired();
        b.Property(p => p.City).HasMaxLength(60).IsRequired();
        b.Property(p => p.District).HasMaxLength(60).IsRequired();
        b.Property(p => p.AddressLine).HasMaxLength(Prison.AddressMax).IsRequired();
        b.Property(p => p.PostalCode).HasMaxLength(5).IsRequired();
        b.Property(p => p.Phone).HasMaxLength(60);
        b.HasIndex(p => new { p.City, p.IsActive, p.Name });
    }
}

internal sealed class PrisonRepository(AppDbContext db) : IPrisonRepository
{
    private DbSet<Prison> Set => db.Set<Prison>();

    public Task<Prison?> GetAsync(Guid id, CancellationToken ct = default) => Set.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Prison>> ListActiveByCityAsync(string city, CancellationToken ct = default) =>
        await Set.AsNoTracking().Where(p => p.City == city && p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Prison>> SearchAsync(string? city, string? query, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var t = query.Trim();
            q = q.Where(p => p.Name.Contains(t) || p.AddressLine.Contains(t) || p.District.Contains(t));
        }
        return await q.OrderBy(p => p.City).ThenBy(p => p.Name).ToListAsync(ct);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default) => Set.AnyAsync(ct);
    public void Add(Prison prison) => Set.Add(prison);
    public void AddRange(IEnumerable<Prison> prisons) => Set.AddRange(prisons);
    public void Remove(Prison prison) => Set.Remove(prison);
}
