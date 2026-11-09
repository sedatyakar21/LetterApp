using System.Text;
using System.Text.RegularExpressions;

namespace MektupApp.Domain.Common;

/// <summary>URL'de kullanılacak adres: küçük harf, Türkçe karakterler sadeleşmiş, kelimeler tireyle ayrılmış.</summary>
public static partial class Slug
{
    public const int MaxLength = 120;

    public static string From(string? text)
    {
        var sb = new StringBuilder();
        foreach (var ch in (text ?? string.Empty).Trim())
        {
            sb.Append(ch switch
            {
                'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'I' or 'İ' or 'i' => 'i',
                'ö' or 'Ö' => 'o', 'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(ch)
            });
        }
        var slug = NonAlnum().Replace(sb.ToString(), "-").Trim('-');
        if (slug.Length > MaxLength) slug = slug[..MaxLength].TrimEnd('-');
        return slug;
    }

    /// <summary>Sonuna "-2", "-3" … ekler; toplam uzunluk sınırı korunur.</summary>
    public static string WithSuffix(string baseSlug, int n)
    {
        var suffix = $"-{n}";
        var head = baseSlug.Length + suffix.Length > MaxLength ? baseSlug[..(MaxLength - suffix.Length)] : baseSlug;
        return head.TrimEnd('-') + suffix;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlnum();
}
