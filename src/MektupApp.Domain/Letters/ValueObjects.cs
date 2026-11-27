using System.Text.RegularExpressions;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

/// <summary>
/// Posta adresi. Kurucu eksik adresi kabul etmez; yalnızca taslak mektupta <see cref="Draft"/> ile eksik adres tutulabilir
/// (ödemeden önce <see cref="IsComplete"/> aranır).
/// </summary>
public sealed partial record PostalAddress
{
    public string Line { get; private init; } = string.Empty;
    public string District { get; private init; } = string.Empty;
    public string City { get; private init; } = string.Empty;
    public string PostalCode { get; private init; } = string.Empty;

    private PostalAddress() { } // EF Core (taslakta eksik adres de yüklenebilsin) ve Draft

    /// <param name="postalCode">İsteğe bağlı (formlarda şimdilik gizli); girilirse 5 haneli olmalıdır. Boşsa "" saklanır.</param>
    public PostalAddress(string line, string district, string city, string? postalCode)
    {
        Line = Guard.Required(line, "Adres", 250);
        District = Guard.Required(district, "İlçe", 60);
        City = Guard.Required(city, "İl", 60);
        PostalCode = NormalizePostalCode(postalCode);
    }

    /// <summary>Taslak için: alanlar boş olabilir (uzunluk ve posta kodu biçimi yine denetlenir).</summary>
    public static PostalAddress Draft(string? line, string? district, string? city, string? postalCode) => new()
    {
        Line = Guard.Optional(line, "Adres", 250) ?? string.Empty,
        District = Guard.Optional(district, "İlçe", 60) ?? string.Empty,
        City = Guard.Optional(city, "İl", 60) ?? string.Empty,
        PostalCode = NormalizePostalCode(postalCode)
    };

    public static PostalAddress Empty { get; } = new();

    public bool IsComplete => Line.Length > 0 && District.Length > 0 && City.Length > 0;

    private static string NormalizePostalCode(string? postalCode)
    {
        var code = postalCode?.Trim() ?? string.Empty;
        if (code.Length > 0 && !PostalCodeRegex().IsMatch(code))
            throw new DomainException("Posta kodu 5 haneli olmalıdır.");
        return code;
    }

    public override string ToString() =>
        PostalCode.Length == 0 ? $"{Line}, {District} / {City}" : $"{Line}, {District} / {City} {PostalCode}";

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex PostalCodeRegex();
}

/// <summary>
/// Alıcı. Askere giden mektupta birlik bilgisi (<see cref="Detail"/>) zorunludur. Cezaevine giden mektupta
/// alıcının baba adı ve koğuş numarası isteğe bağlıdır ama tavsiye edilir (mektubun doğru kişiye ulaşmasını kolaylaştırır).
/// Cezaevinde <see cref="Detail"/> kurum adıdır (seçilen cezaevinden gelir).
/// </summary>
public sealed record Recipient
{
    public const int FatherNameMax = 60;
    public const int WardMax = 30;

    public string FullName { get; private init; }
    public RecipientType Type { get; private init; }
    /// <summary>Asker: birlik bilgisi. Cezaevi: kurum adı. Diğer: boş.</summary>
    public string? Detail { get; private init; }
    /// <summary>Cezaevi alıcısının baba adı (kurumlar aynı adlı hükümlüleri ayırt etmek için ister).</summary>
    public string? FatherName { get; private init; }
    /// <summary>Cezaevi alıcısının koğuş numarası (ör. "B-12").</summary>
    public string? Ward { get; private init; }
    public PostalAddress Address { get; private init; }

    /// <summary>Gönderilebilir mi: ad, eksiksiz adres ve askerde birlik bilgisi (taslakta eksik olabilir).</summary>
    public bool IsComplete =>
        FullName.Length > 0 && Address.IsComplete && (Type != RecipientType.Soldier || Detail is not null);

    /// <summary>
    /// Taslak mektup için alıcı: ad / adres / birlik bilgisi henüz boş olabilir; uzunluklar yine denetlenir.
    /// Ödemeden önce <see cref="IsComplete"/> aranır.
    /// </summary>
    public static Recipient Draft(string? fullName, RecipientType type, string? detail, PostalAddress? address,
        string? fatherName = null, string? ward = null) => new()
    {
        FullName = Guard.Optional(fullName, "Alıcı adı", 100) ?? string.Empty,
        Type = type,
        Detail = Guard.Optional(detail, "Alıcı ek bilgisi", 200),
        Address = address ?? PostalAddress.Empty,
        FatherName = type == RecipientType.Inmate ? Guard.Optional(fatherName, "Baba adı", FatherNameMax) : null,
        Ward = type == RecipientType.Inmate ? Guard.Optional(ward, "Koğuş numarası", WardMax) : null
    };

    // EF Core, iç içe complex type'ları constructor üzerinden bağlayamadığı için gerekli.
    private Recipient()
    {
        FullName = null!;
        Address = null!;
    }

    public Recipient(string fullName, RecipientType type, string? detail, PostalAddress address,
        string? fatherName = null, string? ward = null)
    {
        FullName = Guard.Required(fullName, "Alıcı adı", 100);
        Type = type;
        Detail = Guard.Optional(detail, "Alıcı ek bilgisi", 200);
        Address = address ?? throw new DomainException("Alıcı adresi zorunludur.");
        // Baba adı ve koğuş yalnızca cezaevi alıcısında anlamlıdır.
        FatherName = type == RecipientType.Inmate ? Guard.Optional(fatherName, "Baba adı", FatherNameMax) : null;
        Ward = type == RecipientType.Inmate ? Guard.Optional(ward, "Koğuş numarası", WardMax) : null;

        if (type == RecipientType.Soldier && Detail is null)
            throw new DomainException("Askere gönderilen mektuplarda birlik bilgisi zorunludur.");
    }

    /// <summary>Yalnızca adresi değişen alıcı (diğer bilgiler aynen korunur, eski kayıtlarda kural yeniden işletilmez).</summary>
    public Recipient WithAddress(PostalAddress address) =>
        this with { Address = address ?? throw new DomainException("Alıcı adresi zorunludur.") };

    /// <summary>Zarfta ve PTT kaydında adın altına yazılan ek bilgiler (ör. "Baba adı: Ali · Koğuş: B-12").</summary>
    public string? Note
    {
        get
        {
            var parts = new List<string>();
            if (FatherName is not null) parts.Add($"Baba adı: {FatherName}");
            if (Ward is not null) parts.Add($"Koğuş: {Ward}");
            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }
}

/// <summary>Mektubun görünümü: kağıt, zarf, yazı tipi, mürekkep ve yazı boyutu.</summary>
public sealed record LetterStyle(PaperColor Paper, EnvelopeColor Envelope, LetterFont Font, InkColor Ink,
    TextSize Size = TextSize.Medium)
{
    public static LetterStyle Default { get; } =
        new(PaperColor.White, EnvelopeColor.White, LetterFont.Serif, InkColor.Black);

    public bool HasColoredPaper => Paper != PaperColor.White;
    public bool HasColoredEnvelope => Envelope != EnvelopeColor.White;
}

/// <summary>Mektuba sürülen koku: katalogdaki kimliği ve o anki adı (anlık kopya).</summary>
public sealed record LetterScent
{
    public Guid? ScentId { get; }
    public string Name { get; }

    public LetterScent(Guid? scentId, string name)
    {
        ScentId = scentId;
        Name = Guard.Required(name, "Koku adı", Scents.ScentOption.NameMax);
    }

    public static LetterScent From(Scents.ScentOption scent) => new(scent.Id, scent.Name);
}

/// <summary>
/// Mektuba eklenen kartpostal: katalogdaki kartpostalın kimliği ile o anki adı ve kategorisi (anlık kopya).
/// Katalogdan çıkarılsa da sipariş ekranında / baskıda hangi kartpostalın konacağı bilinir.
/// </summary>
/// <param name="DesignId">Katalogdaki kartpostal; eski (hazır tasarımlı) siparişlerde boştur.</param>
public sealed record Postcard
{
    public Guid? DesignId { get; }
    public string Title { get; }
    public string Category { get; }

    public Postcard(Guid? designId, string title, string category)
    {
        DesignId = designId;
        Title = Guard.Required(title, "Kartpostal adı", Postcards.PostcardDesign.TitleMax);
        Category = Guard.Required(category, "Kartpostal kategorisi", Postcards.PostcardDesign.CategoryMax);
    }

    public static Postcard From(Postcards.PostcardDesign design) => new(design.Id, design.Title, design.Category);

    /// <summary>"Gül Bahçesi (Aşk)" biçiminde.</summary>
    public string DisplayText => $"{Title} ({Category})";
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
