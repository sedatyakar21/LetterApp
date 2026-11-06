using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace MektupApp.Domain.Customers;

/// <summary>"Bizi nereden duydunuz?" seçenekleri.</summary>
public enum AcquisitionChannel
{
    [Display(Name = "Sosyal medya")] SocialMedia = 1,
    [Display(Name = "Radyo")] Radio = 2,
    [Display(Name = "Arkadaş")] Friend = 3,
    [Display(Name = "Diğer")] Other = 4
}

/// <summary>
/// Akılda kalıcı referans numarası: ad ve soyadın baş harfleri + 6 haneli sayı.
/// Örnek: "Ayşe Yılmaz" → AY482915. Türkçe harfler sadeleştirilir (Ş→S, İ→I …),
/// böylece kod her klavyeden yazılabilir.
/// </summary>
public static class ReferralCode
{
    public const int Length = 8;          // 2 harf + 6 rakam
    private const int DigitCount = 6;

    public static string Generate(string fullName)
    {
        var number = RandomNumberGenerator.GetInt32(100_000, 1_000_000); // baştaki 0 karışıklığı olmasın
        return Initials(fullName) + number.ToString(new string('0', DigitCount));
    }

    /// <summary>
    /// İlk ve son kelimenin baş harfleri ("Ali Veli Can" → AC). Tek kelimeyse ilk iki harf ("Cem" → CE).
    /// </summary>
    public static string Initials(string? fullName)
    {
        var words = ToAscii(fullName)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsAsciiLetter).ToArray()))
            .Where(w => w.Length > 0)
            .ToList();

        var initials = words.Count switch
        {
            0 => "XX",
            1 => (words[0] + "X")[..2],
            _ => $"{words[0][0]}{words[^1][0]}"
        };
        return initials.ToUpperInvariant();
    }

    /// <summary>Kullanıcının yazdığı kodu karşılaştırılabilir hale getirir (boşluk, küçük harf, Türkçe harf toleransı).</summary>
    public static string Normalize(string? code) =>
        ToAscii(code).Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();

    private static string ToAscii(string? text)
    {
        var sb = new StringBuilder((text ?? string.Empty).Length);
        foreach (var ch in text ?? string.Empty)
        {
            sb.Append(ch switch
            {
                'ç' or 'Ç' => 'C',
                'ğ' or 'Ğ' => 'G',
                'ı' or 'I' or 'i' or 'İ' => 'I',
                'ö' or 'Ö' => 'O',
                'ş' or 'Ş' => 'S',
                'ü' or 'Ü' => 'U',
                _ => ch
            });
        }
        return sb.ToString();
    }
}
