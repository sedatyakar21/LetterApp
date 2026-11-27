using MektupApp.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> b)
    {
        b.ToTable("LegalDocuments");
        b.HasKey(d => d.Id);
        b.Ignore(d => d.DomainEvents);
        b.Property(d => d.Slug).HasMaxLength(LegalDocument.SlugMax).IsUnicode(false).IsRequired();
        b.HasIndex(d => d.Slug).IsUnique();
        b.Property(d => d.Title).HasMaxLength(LegalDocument.TitleMax).IsRequired();
        b.Property(d => d.ContentMarkdown).IsRequired();
        b.Property(d => d.UpdatedBy).HasMaxLength(200);
    }
}

internal sealed class LegalDocumentRepository(AppDbContext db) : ILegalDocumentRepository
{
    public Task<LegalDocument?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Set<LegalDocument>().FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<LegalDocument?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Set<LegalDocument>().FirstOrDefaultAsync(d => d.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken ct = default) =>
        db.Set<LegalDocument>().AnyAsync(d => d.Slug == slug && d.Id != exceptId, ct);

    public async Task<IReadOnlyList<LegalDocument>> ListAsync(CancellationToken ct = default) =>
        await db.Set<LegalDocument>().OrderBy(d => d.SortOrder).ThenBy(d => d.Title).ToListAsync(ct);

    public void Add(LegalDocument document) => db.Set<LegalDocument>().Add(document);

    public void Remove(LegalDocument document) => db.Set<LegalDocument>().Remove(document);
}
