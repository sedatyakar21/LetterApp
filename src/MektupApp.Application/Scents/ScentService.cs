using MektupApp.Application.Abstractions;
using MektupApp.Domain.Scents;

namespace MektupApp.Application.Scents;

public sealed record ScentDto(Guid Id, string Name, bool IsActive, int SortOrder, bool IsUsed = false);

public sealed record SaveScentCommand(string Name, int SortOrder, bool IsActive);

/// <summary>Koku listesi: yönetim panelinden girilir, müşteri mektup yazarken aktif olanlardan seçer.</summary>
public sealed class ScentService(IScentRepository scents, IUnitOfWork unitOfWork)
{
    /// <summary>Müşteriye gösterilenler (aktif), sırasıyla.</summary>
    public async Task<IReadOnlyList<ScentDto>> ListActiveAsync(CancellationToken ct = default) =>
        (await scents.ListAsync(activeOnly: true, ct)).Select(s => new ScentDto(s.Id, s.Name, s.IsActive, s.SortOrder)).ToList();

    /// <summary>Yönetim paneli: hepsi, kullanımda olup olmadığıyla (kullanılan silinemez, pasife alınır).</summary>
    public async Task<IReadOnlyList<ScentDto>> ListAllAsync(CancellationToken ct = default)
    {
        var list = await scents.ListAsync(activeOnly: false, ct);
        var result = new List<ScentDto>(list.Count);
        foreach (var s in list) result.Add(new ScentDto(s.Id, s.Name, s.IsActive, s.SortOrder, await scents.IsUsedAsync(s.Id, ct)));
        return result;
    }

    public async Task<ScentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var s = await LoadAsync(id, ct);
        return new ScentDto(s.Id, s.Name, s.IsActive, s.SortOrder, await scents.IsUsedAsync(id, ct));
    }

    public async Task<Guid> CreateAsync(SaveScentCommand cmd, CancellationToken ct = default)
    {
        await EnsureUniqueAsync(cmd.Name, null, ct);
        var s = ScentOption.Create(cmd.Name, cmd.SortOrder, cmd.IsActive);
        scents.Add(s);
        await unitOfWork.SaveChangesAsync(ct);
        return s.Id;
    }

    public async Task UpdateAsync(Guid id, SaveScentCommand cmd, CancellationToken ct = default)
    {
        var s = await LoadAsync(id, ct);
        await EnsureUniqueAsync(cmd.Name, id, ct);
        s.Update(cmd.Name, cmd.SortOrder, cmd.IsActive);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        (await LoadAsync(id, ct)).SetActive(isActive);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Hiçbir mektupta seçilmemişse siler; seçilmişse pasife alınmalıdır.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await LoadAsync(id, ct);
        if (await scents.IsUsedAsync(id, ct))
            throw new AppException("Bu koku mektuplarda seçildiği için silinemez. Müşterilere gösterilmesin istiyorsanız pasife alın.");
        scents.Remove(s);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(name) && await scents.NameExistsAsync(name.Trim(), exceptId, ct))
            throw new AppException("Bu adda bir koku zaten var.");
    }

    private async Task<ScentOption> LoadAsync(Guid id, CancellationToken ct) =>
        await scents.GetAsync(id, ct) ?? throw new AppException("Koku bulunamadı.");
}
