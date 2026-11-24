using MektupApp.Domain.Common;

namespace MektupApp.Domain.Scents;

/// <summary>
/// Mektuba sürülebilen bir koku (yönetim panelinden eklenir). Müşteri mektup yazarken aktif kokulardan birini seçer;
/// mektupta kokunun adı anlık kopya olarak saklanır, böylece koku sonradan değişse de sipariş bilgisi bozulmaz.
/// </summary>
public sealed class ScentOption : AggregateRoot
{
    public const int NameMax = 50;

    // Önceden koda gömülü olan kokular (migration bu kimliklerle ekler; eski siparişler bunlara bağlanır).
    public static readonly Guid MagnoliaId = new("5ce47000-0000-0000-0000-000000000001");
    public static readonly Guid LavenderId = new("5ce47000-0000-0000-0000-000000000002");
    public static readonly Guid SpringFlowersId = new("5ce47000-0000-0000-0000-000000000003");

    private ScentOption() { } // EF Core

    public string Name { get; private set; } = string.Empty;
    /// <summary>Pasif koku müşteriye gösterilmez; onu seçmiş mektuplar etkilenmez.</summary>
    public bool IsActive { get; private set; }
    /// <summary>Küçük sayı önce gösterilir.</summary>
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static ScentOption Create(string name, int sortOrder = 0, bool isActive = true)
    {
        var s = new ScentOption { CreatedAt = DateTime.UtcNow };
        s.Update(name, sortOrder, isActive);
        return s;
    }

    public void Update(string name, int sortOrder, bool isActive)
    {
        Name = Guard.Required(name, "Koku adı", NameMax);
        if (sortOrder is < 0 or > 9999) throw new DomainException("Sıra 0 ile 9999 arasında olmalıdır.");
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}

public interface IScentRepository
{
    Task<ScentOption?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sıraya, sonra ada göre.</summary>
    Task<IReadOnlyList<ScentOption>> ListAsync(bool activeOnly, CancellationToken ct = default);
    /// <summary>Bu kokuyu seçmiş (silinmemiş) mektup var mı.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default);
    /// <summary>Aynı adda (büyük/küçük harf farkı gözetmeden) başka koku var mı.</summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct = default);
    void Add(ScentOption scent);
    void Remove(ScentOption scent);
}
