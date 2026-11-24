namespace MektupApp.Domain.Letters;

/// <summary>
/// Sipariş numarası: mektup kimliğinin ilk 8 karakteri, büyük harfle (ör. "04F731B2").
/// Ekranlarda "#04F731B2" olarak gösterilir; yönetim panelinde bununla arama yapılabilir.
/// </summary>
public static class OrderNumber
{
    public const int Length = 8;

    public static string Format(Guid letterId) => letterId.ToString("N")[..Length].ToUpperInvariant();

    /// <summary>
    /// Arama metni bir sipariş numarası (veya başı) gibi görünüyorsa büyük harfli halini döner; değilse null.
    /// "#" ile başlıyorsa en az 4, başında "#" yoksa (isimlerle karışmasın diye) en az 6 onaltılık karakter gerekir.
    /// </summary>
    public static string? AsSearchPrefix(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var q = query.Trim().Replace(" ", "");
        var hasHash = q.StartsWith('#');
        if (hasHash) q = q[1..];
        if (q.Length > Length || q.Length < (hasHash ? 4 : 6) || !q.All(Uri.IsHexDigit)) return null;
        return q.ToUpperInvariant();
    }
}
