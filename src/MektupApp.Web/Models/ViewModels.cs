using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Letters;
using MektupApp.Domain.Letters;

namespace MektupApp.Web.Models;

/// <summary>Şifremi unuttum: e-posta adresi ve gönderim yöntemi.</summary>
public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "Üyelikte kullandığınız e-posta")]
    public string Email { get; set; } = string.Empty;

    public MektupApp.Domain.Customers.PasswordResetMethod Method { get; set; }

    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
}

/// <summary>Yeni şifre formu (e-posta bağlantısıyla veya SMS koduyla).</summary>
public sealed class ResetPasswordViewModel
{
    /// <summary>E-posta bağlantısındaki anahtar (SMS akışında boş).</summary>
    public string? Token { get; set; }

    /// <summary>SMS akışında: kod gönderilen hesabın e-postası.</summary>
    public string? Email { get; set; }

    [Display(Name = "SMS ile gelen kod")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Yeni şifre zorunludur."), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [Display(Name = "Yeni şifre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı zorunludur."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [Display(Name = "Yeni şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur."), DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur."), StringLength(100)]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cep telefonu zorunludur.")]
    [Display(Name = "Cep Telefonu"), DataType(DataType.PhoneNumber)]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur."), MinLength(8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
    [DataType(DataType.Password), Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password), Display(Name = "Şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool AcceptTerms { get; set; }

    [Display(Name = "Bizi nereden duydunuz?")]
    public MektupApp.Domain.Customers.AcquisitionChannel? Channel { get; set; }

    [StringLength(20), Display(Name = "Arkadaşınızın referans no'su")]
    public string? ReferralCode { get; set; }

    [Display(Name = "Doğum tarihi")]
    public DateOnly? BirthDate { get; set; }
}

public sealed class AddressInput
{
    [Required(ErrorMessage = "Adres zorunludur."), StringLength(250), Display(Name = "Adres")]
    public string Line { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlçe zorunludur."), StringLength(60), Display(Name = "İlçe")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "İl zorunludur."), StringLength(60), Display(Name = "İl")]
    public string City { get; set; } = string.Empty;

    /// <summary>İsteğe bağlı; formda şimdilik gizli (kayıtlı değer gizli alanda korunur).</summary>
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli olmalıdır.")]
    [Display(Name = "Posta Kodu")]
    public string? PostalCode { get; set; }

    public AddressDto ToDto() => new(Line, District, City, PostalCode ?? string.Empty);

    public static AddressInput From(AddressDto a) =>
        new() { Line = a.Line, District = a.District, City = a.City, PostalCode = a.PostalCode };
}

/// <summary>_AddressFields parçasının modeli.</summary>
/// <param name="Prison">Alıcı adresinde cezaevi seçimi (yalnızca alıcı adresi için; gönderende null).</param>
public sealed record AddressFieldsModel(string Prefix, AddressInput Address, PrisonPickerOptions? Prison = null);

/// <param name="TypeSelector">Alıcı türü açılır listesinin CSS seçicisi (değişince cezaevi listesi gösterilir / gizlenir); yoksa IsInmate sabittir.</param>
/// <param name="InmateValue">"Cezaevi" seçeneğinin değeri.</param>
/// <param name="DetailSelector">Kurum adının yazılacağı "Kurum ve koğuş bilgisi" alanı (boşsa doldurulur).</param>
public sealed record PrisonPickerOptions(bool IsInmate, string? TypeSelector = null, string? DetailSelector = null)
{
    public string InmateValue => ((int)RecipientType.Inmate).ToString();
}

/// <summary>Mektuptaki bir kartpostal (formda gizli alanlarla taşınır).</summary>
public sealed class PostcardChoice
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>"Mektup Yaz" formu. Hem yeni taslak hem düzenleme için kullanılır.</summary>
public sealed class LetterFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Gönderen adı zorunludur."), StringLength(100), Display(Name = "Gönderen Ad Soyad")]
    public string SenderName { get; set; } = string.Empty;

    public AddressInput SenderAddress { get; set; } = new();

    [Required(ErrorMessage = "Alıcı adı zorunludur."), StringLength(100), Display(Name = "Alıcı Ad Soyad")]
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Yeni mektupta varsayılan alıcı türü cezaevidir (müşterilerin çoğu cezaevine yazıyor).</summary>
    [Display(Name = "Alıcı Türü")]
    public RecipientType RecipientType { get; set; } = RecipientType.Inmate;

    /// <summary>Asker: birlik bilgisi (formda görünür). Cezaevi: kurum adı (seçilen cezaevinden gelir, gizli alan).</summary>
    [StringLength(200), Display(Name = "Ek Bilgi")]
    public string? RecipientDetail { get; set; }

    [StringLength(Recipient.FatherNameMax), Display(Name = "Alıcının baba adı")]
    public string? RecipientFatherName { get; set; }

    [StringLength(Recipient.WardMax), Display(Name = "Koğuş numarası")]
    public string? RecipientWard { get; set; }

    public AddressInput RecipientAddress { get; set; } = new();

    [StringLength(Letter.MaxContentLength), Display(Name = "Mektubunuz")]
    public string? Content { get; set; }

    [Display(Name = "Kağıt Rengi")] public PaperColor Paper { get; set; }
    [Display(Name = "Zarf Rengi")] public EnvelopeColor Envelope { get; set; }
    [Display(Name = "Yazı Stili")] public LetterFont Font { get; set; }
    [Display(Name = "Mürekkep")] public InkColor Ink { get; set; }
    [Display(Name = "Yazı Boyutu")] public TextSize Size { get; set; } = TextSize.Medium;
    /// <summary>Yönetim panelinden girilen kokulardan seçilen (boş: kokusuz).</summary>
    [Display(Name = "Koku")] public Guid? ScentId { get; set; }
    /// <summary>Mektuptaki kokunun adı: koku sonradan pasife alındıysa listede yine gösterilir.</summary>
    public string? ScentName { get; set; }
    /// <summary>Katalogdan seçilen kartpostallar (boş: kartpostal istenmiyor).</summary>
    [Display(Name = "Kartpostal")] public List<Guid> PostcardIds { get; set; } = [];
    /// <summary>Mektupta kayıtlı kartpostallar: katalogda pasife alınmış olsalar da formda gösterilir (seçim kaybolmasın).</summary>
    public List<PostcardChoice> CurrentPostcards { get; set; } = [];

    public int PhotoCount { get; set; }

    public int DocumentCount { get; set; }

    /// <summary>Ekstralar sekmesinde listelenen fotoğraf ve belgeler (yalnızca gösterim; sayfa içinde yüklenir / silinir).</summary>
    public IReadOnlyList<LetterPhotoDto> Photos { get; set; } = [];
    public IReadOnlyList<LetterDocumentDto> Documents { get; set; } = [];

    public SaveLetterCommand ToCommand(Guid customerId) => new(
        Id, customerId, SenderName, SenderAddress.ToDto(),
        RecipientName, RecipientType, RecipientDetail, RecipientAddress.ToDto(),
        Content, Paper, Envelope, Font, Ink, ScentId, PostcardIds,
        RecipientFatherName, RecipientWard, Size);

    /// <summary>Aynı türdeki son mektubun alıcı / gönderen bilgilerini forma yazar (yeni mektupta).</summary>
    public void ApplyPrefill(LetterPrefillDto p)
    {
        RecipientType = p.RecipientType;
        RecipientName = p.RecipientName;
        RecipientDetail = p.RecipientDetail;
        RecipientFatherName = p.RecipientFatherName;
        RecipientWard = p.RecipientWard;
        RecipientAddress = AddressInput.From(p.RecipientAddress);
        SenderName = p.SenderName;
        SenderAddress = AddressInput.From(p.SenderAddress);
    }

    public static LetterFormViewModel From(LetterDetailsDto d) => new()
    {
        Id = d.Id,
        SenderName = d.SenderName,
        SenderAddress = AddressInput.From(d.SenderAddress),
        RecipientName = d.RecipientName,
        RecipientType = d.RecipientType,
        RecipientDetail = d.RecipientDetail,
        RecipientFatherName = d.RecipientFatherName,
        RecipientWard = d.RecipientWard,
        RecipientAddress = AddressInput.From(d.RecipientAddress),
        Content = d.Content,
        Paper = d.Style.Paper,
        Envelope = d.Style.Envelope,
        Font = d.Style.Font,
        Ink = d.Style.Ink,
        Size = d.Style.Size,
        ScentId = d.Scent?.ScentId,
        ScentName = d.Scent?.Name,
        PostcardIds = d.Postcards.Where(p => p.DesignId is not null).Select(p => p.DesignId!.Value).ToList(),
        CurrentPostcards = d.Postcards.Where(p => p.DesignId is not null)
            .Select(p => new PostcardChoice { Id = p.DesignId!.Value, Title = p.Title, Category = p.Category }).ToList(),
        PhotoCount = d.Photos.Count,
        DocumentCount = d.Documents.Count,
        Photos = d.Photos,
        Documents = d.Documents
    };
}

public sealed class CheckoutViewModel
{
    public required LetterDetailsDto Letter { get; init; }
    public required QuoteDto Quote { get; init; }
}

/// <summary>Hesabım'da e-posta veya cep telefonu satırı: değiştir formu ve onay kodu alanı.</summary>
public sealed class ContactFieldModel
{
    public MektupApp.Domain.Customers.ContactChangeKind Kind { get; init; }
    public string Current { get; init; } = "";
    public bool HasPassword { get; init; }
    /// <summary>Kodu gönderilmiş, onay bekleyen değişiklik.</summary>
    public MektupApp.Application.Customers.PendingContactChangeDto? Pending { get; init; }
    /// <summary>Hatalı bir denemeden dönüldü: form açık gelir.</summary>
    public bool Open { get; init; }
    public string? Entered { get; init; }
    public bool IsEmail => Kind == MektupApp.Domain.Customers.ContactChangeKind.Email;
}

public sealed class PendingPaymentViewModel
{
    public required LetterDetailsDto Letter { get; init; }
    public MektupApp.Application.Payments.PaymentStatusDto? Payment { get; init; }
    /// <summary>Havale / EFT bekleniyorsa müşterinin göndereceği hesap ve tutar.</summary>
    public MektupApp.Application.Payments.BankTransferInstructionsDto? BankTransfer { get; init; }
}

public sealed class ShipInput
{
    [Required(ErrorMessage = "Takip kodu zorunludur.")]
    public string TrackingCode { get; set; } = string.Empty;
}
