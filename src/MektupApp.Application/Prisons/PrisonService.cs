using MektupApp.Application.Abstractions;
using MektupApp.Domain.Prisons;

namespace MektupApp.Application.Prisons;

/// <summary>Mektup formundaki cezaevi listesi için (yalnızca aktif kurumlar).</summary>
public sealed record PrisonOptionDto(Guid Id, string Name, string City, string District, string AddressLine, string PostalCode);

public sealed record PrisonDto(Guid Id, string Name, string City, string District, string AddressLine, string PostalCode,
    string? Phone, bool IsActive, DateTime UpdatedAt);

public sealed record SavePrisonCommand(string Name, string City, string? District, string? AddressLine, string? PostalCode,
    string? Phone, bool IsActive);

/// <summary>Başlangıç verisindeki bir kayıt (Infrastructure/Seed/cezaevleri.json).</summary>
public sealed record PrisonSeedItem(string Name, string City, string? District, string? AddressLine, string? PostalCode, string? Phone);

public sealed class PrisonService(IPrisonRepository prisons, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PrisonOptionDto>> ListForCityAsync(string? city, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(city)) return [];
        return (await prisons.ListActiveByCityAsync(city.Trim(), ct))
            .Select(p => new PrisonOptionDto(p.Id, p.Name, p.City, p.District, p.AddressLine, p.PostalCode))
            .ToList();
    }

    // ---------------- Yönetim ----------------

    public async Task<IReadOnlyList<PrisonDto>> SearchAsync(string? city, string? query, CancellationToken ct = default) =>
        (await prisons.SearchAsync(city, query, ct)).Select(ToDto).ToList();

    public async Task<PrisonDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await prisons.GetAsync(id, ct) ?? throw new AppException("Kurum bulunamadı."));

    public async Task<Guid> CreateAsync(SavePrisonCommand c, CancellationToken ct = default)
    {
        var p = Prison.Create(c.Name, c.City, c.District, c.AddressLine, c.PostalCode, c.Phone);
        if (!c.IsActive) p.Update(c.Name, c.City, c.District, c.AddressLine, c.PostalCode, c.Phone, isActive: false);
        prisons.Add(p);
        await unitOfWork.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdateAsync(Guid id, SavePrisonCommand c, CancellationToken ct = default)
    {
        var p = await prisons.GetAsync(id, ct) ?? throw new AppException("Kurum bulunamadı.");
        p.Update(c.Name, c.City, c.District, c.AddressLine, c.PostalCode, c.Phone, c.IsActive);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Kalıcı silme. Kayıtlı mektupların adresi mektupta saklandığı için etkilenmez.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await prisons.GetAsync(id, ct) ?? throw new AppException("Kurum bulunamadı.");
        prisons.Remove(p);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Tablo boşsa başlangıç verisini yükler (ilk kurulum). Dolu tabloya dokunmaz.</summary>
    public async Task<int> SeedIfEmptyAsync(IEnumerable<PrisonSeedItem> items, CancellationToken ct = default)
    {
        if (await prisons.AnyAsync(ct)) return 0;
        var list = items.Select(i => Prison.Create(i.Name, i.City, i.District, i.AddressLine, i.PostalCode, i.Phone)).ToList();
        prisons.AddRange(list);
        await unitOfWork.SaveChangesAsync(ct);
        return list.Count;
    }

    private static PrisonDto ToDto(Prison p) =>
        new(p.Id, p.Name, p.City, p.District, p.AddressLine, p.PostalCode, p.Phone, p.IsActive, p.UpdatedAt);
}
