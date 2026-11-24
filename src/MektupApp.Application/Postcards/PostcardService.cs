using MektupApp.Application.Abstractions;
using MektupApp.Application.Letters;
using MektupApp.Domain.Common;
using MektupApp.Domain.Postcards;

namespace MektupApp.Application.Postcards;

public sealed record PostcardDto(Guid Id, string Title, string Category, bool IsActive, int SortOrder, DateTime UpdatedAt, bool IsUsed = false);

public sealed record SavePostcardCommand(string Title, string Category, int SortOrder, bool IsActive);

/// <summary>Kartpostal kataloğu: yönetim panelinden eklenir, müşteri mektup yazarken aktif olanlardan seçer.</summary>
public sealed class PostcardService(IPostcardRepository postcards, IFileStorage files, IUnitOfWork unitOfWork)
{
    public static readonly IReadOnlyList<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public const long MaxImageBytes = 5 * 1024 * 1024;

    /// <summary>Müşteriye gösterilenler (aktif), sırasıyla.</summary>
    public async Task<IReadOnlyList<PostcardDto>> ListActiveAsync(CancellationToken ct = default) =>
        (await postcards.ListAsync(activeOnly: true, ct)).Select(p => ToDto(p)).ToList();

    /// <summary>Yönetim paneli: hepsi, kullanımda olup olmadığıyla (kullanılan silinemez, pasife alınır).</summary>
    public async Task<IReadOnlyList<PostcardDto>> ListAllAsync(CancellationToken ct = default)
    {
        var list = await postcards.ListAsync(activeOnly: false, ct);
        var result = new List<PostcardDto>(list.Count);
        foreach (var p in list) result.Add(ToDto(p, await postcards.IsUsedAsync(p.Id, ct)));
        return result;
    }

    public async Task<PostcardDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        return ToDto(p, await postcards.IsUsedAsync(id, ct));
    }

    /// <summary>Mevcut kategoriler (yönetim formunda öneri listesi).</summary>
    public async Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct = default) =>
        (await postcards.ListAsync(activeOnly: false, ct)).Select(p => p.Category).Distinct()
            .Order(StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true)).ToList();

    public async Task<Guid> CreateAsync(SavePostcardCommand cmd, Stream image, string fileName, long length, CancellationToken ct = default)
    {
        var (buffer, ext) = await ReadImageAsync(image, fileName, length, ct);
        string key;
        await using (buffer) key = await files.SaveAsync(buffer, ext, ct);
        try
        {
            var p = PostcardDesign.Create(cmd.Title, cmd.Category, key, cmd.SortOrder, cmd.IsActive);
            postcards.Add(p);
            await unitOfWork.SaveChangesAsync(ct);
            return p.Id;
        }
        catch
        {
            await files.DeleteAsync(key, ct);
            throw;
        }
    }

    public async Task UpdateAsync(Guid id, SavePostcardCommand cmd, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        p.Update(cmd.Title, cmd.Category, cmd.SortOrder, cmd.IsActive);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ReplaceImageAsync(Guid id, Stream image, string fileName, long length, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        var (buffer, ext) = await ReadImageAsync(image, fileName, length, ct);
        string key;
        await using (buffer) key = await files.SaveAsync(buffer, ext, ct);
        var old = p.ReplaceImage(key);
        await unitOfWork.SaveChangesAsync(ct);
        await files.DeleteAsync(old, ct);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        (await LoadAsync(id, ct)).SetActive(isActive);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Hiçbir mektupta seçilmemişse siler; seçilmişse görseli siparişlerde gerektiği için silinmez, pasife alınmalıdır.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await LoadAsync(id, ct);
        if (await postcards.IsUsedAsync(id, ct))
            throw new AppException("Bu kartpostal mektuplarda seçildiği için silinemez. Müşterilere gösterilmesin istiyorsanız pasife alın.");
        var key = p.ImageKey;
        postcards.Remove(p);
        await unitOfWork.SaveChangesAsync(ct);
        await files.DeleteAsync(key, ct);
    }

    /// <summary>Kartpostal görseli (pasif olanlar da: eski siparişlerde ve yönetim panelinde gösterilir).</summary>
    public async Task<(Stream Stream, string Key)?> OpenImageAsync(Guid id, CancellationToken ct = default)
    {
        var p = await postcards.GetAsync(id, ct);
        if (p is null) return null;
        var stream = await files.OpenAsync(p.ImageKey, ct);
        return stream is null ? null : (stream, p.ImageKey);
    }

    // ---------------- yardımcılar ----------------

    private async Task<PostcardDesign> LoadAsync(Guid id, CancellationToken ct) =>
        await postcards.GetAsync(id, ct) ?? throw new AppException("Kartpostal bulunamadı.");

    /// <summary>Uzantı, boyut ve dosya imzası (gerçekten JPG / PNG / WEBP mi) kontrol edilir.</summary>
    private static async Task<(MemoryStream Buffer, string Ext)> ReadImageAsync(Stream image, string fileName, long length, CancellationToken ct)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            throw new AppException("Kartpostal görseli JPG, PNG veya WEBP olmalıdır.");
        if (length is <= 0 or > MaxImageBytes)
            throw new AppException("Kartpostal görseli en fazla 5 MB olabilir.");

        var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);
        if (buffer.Length is 0 or > MaxImageBytes)
            throw new AppException("Kartpostal görseli en fazla 5 MB olabilir.");
        if (!ImageSignature.Matches(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 1024)), ext))
            throw new AppException("Dosya içeriği bir görsel değil ya da uzantısıyla uyuşmuyor.");
        buffer.Position = 0;
        return (buffer, ext);
    }

    private static PostcardDto ToDto(PostcardDesign p, bool isUsed = false) =>
        new(p.Id, p.Title, p.Category, p.IsActive, p.SortOrder, p.UpdatedAt, isUsed);
}

/// <summary>Görsel dosya imzası: JPG / PNG (belge kontrolündekiyle aynı) ve WEBP ("RIFF....WEBP").</summary>
public static class ImageSignature
{
    public static bool Matches(ReadOnlySpan<byte> content, string extension) => extension.ToLowerInvariant() switch
    {
        ".webp" => content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8),
        ".jpg" or ".jpeg" or ".png" => DocumentSignature.Matches(content, extension),
        _ => false
    };
}
