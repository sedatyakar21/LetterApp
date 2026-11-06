using System.Text.RegularExpressions;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

/// <summary>Posta adresi. Geçersiz bir adres oluşturulamaz.</summary>
public sealed partial record PostalAddress
{
    public string Line { get; }
    public string District { get; }
    public string City { get; }
    public string PostalCode { get; }

    public PostalAddress(string line, string district, string city, string postalCode)
    {
        Line = Guard.Required(line, "Adres", 250);
        District = Guard.Required(district, "İlçe", 60);
        City = Guard.Required(city, "İl", 60);
        PostalCode = Guard.Required(postalCode, "Posta kodu", 5);
        if (!PostalCodeRegex().IsMatch(PostalCode))
            throw new DomainException("Posta kodu 5 haneli olmalıdır.");
    }

    public override string ToString() => $"{Line}, {District} / {City} {PostalCode}";

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodeRegex();
}

/// <summary>
/// Alıcı. Asker ve cezaevi alıcıları için ek bilgi (birlik / kurum, koğuş vb.) zorunludur.
/// </summary>
public sealed record Recipient
{
    public string FullName { get; }
    public RecipientType Type { get; }
    public string? Detail { get; }
    public PostalAddress Address { get; }

    // EF Core, iç içe complex type'ları constructor üzerinden bağlayamadığı için gerekli.
    private Recipient()
    {
        FullName = null!;
        Address = null!;
    }

    public Recipient(string fullName, RecipientType type, string? detail, PostalAddress address)
    {
        FullName = Guard.Required(fullName, "Alıcı adı", 100);
        Type = type;
        Detail = Guard.Optional(detail, "Alıcı ek bilgisi", 200);
        Address = address ?? throw new DomainException("Alıcı adresi zorunludur.");

        if (type == RecipientType.Soldier && Detail is null)
            throw new DomainException("Askere gönderilen mektuplarda birlik bilgisi zorunludur.");
        if (type == RecipientType.Inmate && Detail is null)
            throw new DomainException("Cezaevine gönderilen mektuplarda kurum ve koğuş bilgisi zorunludur.");
    }
}

/// <summary>Mektubun görünümü: kağıt, zarf, yazı tipi ve mürekkep.</summary>
public sealed record LetterStyle(PaperColor Paper, EnvelopeColor Envelope, LetterFont Font, InkColor Ink)
{
    public static LetterStyle Default { get; } =
        new(PaperColor.White, EnvelopeColor.White, LetterFont.Serif, InkColor.Black);

    public bool HasColoredPaper => Paper != PaperColor.White;
    public bool HasColoredEnvelope => Envelope != EnvelopeColor.White;
}

/// <summary>Mektuba eklenen kartpostal. Her kategoride 1..DesignsPerCategory arası tasarım vardır.</summary>
public sealed record Postcard
{
    public const int DesignsPerCategory = 4;

    public PostcardCategory Category { get; }
    public int Design { get; }

    public Postcard(PostcardCategory category, int design)
    {
        if (!Enum.IsDefined(category))
            throw new DomainException("Geçersiz kartpostal kategorisi.");
        if (design is < 1 or > DesignsPerCategory)
            throw new DomainException("Geçersiz kartpostal tasarımı.");
        Category = category;
        Design = design;
    }
}

/// <summary>
/// PTT gönderi barkodu. İki biçim kabul edilir:
/// UPU S10 (kayıtlı posta, ör. RR123456789TR) ve PTT Kargo (2 harf + 11 rakam, ör. KP12345678901).
/// </summary>
public static partial class TrackingCode
{
    private static readonly int[] S10Weights = [8, 6, 4, 2, 3, 5, 9, 7];

    public static string Normalize(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "");
        if (!S10Format().IsMatch(value) && !CargoFormat().IsMatch(value))
            throw new DomainException("Takip kodu 'RR123456789TR' veya 'KP12345678901' formatında olmalıdır.");
        return value;
    }

    /// <summary>UPU S10 kontrol hanesi: 8 haneli seri numarası için 9. hane.</summary>
    public static int S10CheckDigit(long serial)
    {
        if (serial is < 0 or > 99_999_999)
            throw new DomainException("S10 seri numarası 8 haneli olmalıdır.");
        var digits = serial.ToString("00000000");
        var sum = digits.Select((d, i) => (d - '0') * S10Weights[i]).Sum();
        var check = 11 - sum % 11;
        return check switch { 10 => 0, 11 => 5, _ => check };
    }

    /// <summary>Önek + 8 haneli seri + kontrol hanesi + sonek (ör. RR + 12345678 + 6 + TR).</summary>
    public static string BuildS10(string prefix, long serial, string suffix) =>
        Normalize($"{prefix}{serial:00000000}{S10CheckDigit(serial)}{suffix}");

    [GeneratedRegex(@"^[A-Z]{2}\d{9}[A-Z]{2}$")]
    private static partial Regex S10Format();

    [GeneratedRegex(@"^[A-Z]{2}\d{11}$")]
    private static partial Regex CargoFormat();
}
