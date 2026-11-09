using System.ComponentModel.DataAnnotations;
using MektupApp.Application.Letters;
using MektupApp.Domain.Letters;

namespace MektupApp.Web.Models;

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

    [Required(ErrorMessage = "Posta kodu zorunludur."), RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli olmalıdır.")]
    [Display(Name = "Posta Kodu")]
    public string PostalCode { get; set; } = string.Empty;

    public AddressDto ToDto() => new(Line, District, City, PostalCode);

    public static AddressInput From(AddressDto a) =>
        new() { Line = a.Line, District = a.District, City = a.City, PostalCode = a.PostalCode };
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

    [Display(Name = "Alıcı Türü")]
    public RecipientType RecipientType { get; set; }

    [StringLength(200), Display(Name = "Ek Bilgi")]
    public string? RecipientDetail { get; set; }

    public AddressInput RecipientAddress { get; set; } = new();

    [StringLength(Letter.MaxContentLength), Display(Name = "Mektubunuz")]
    public string? Content { get; set; }

    [Display(Name = "Kağıt Rengi")] public PaperColor Paper { get; set; }
    [Display(Name = "Zarf Rengi")] public EnvelopeColor Envelope { get; set; }
    [Display(Name = "Yazı Stili")] public LetterFont Font { get; set; }
    [Display(Name = "Mürekkep")] public InkColor Ink { get; set; }
    [Display(Name = "Koku")] public Scent Scent { get; set; }
    [Display(Name = "Kartpostal")] public PostcardCategory? PostcardCategory { get; set; }
    public int? PostcardDesign { get; set; }

    public int PhotoCount { get; set; }

    public SaveLetterCommand ToCommand(Guid customerId) => new(
        Id, customerId, SenderName, SenderAddress.ToDto(),
        RecipientName, RecipientType, RecipientDetail, RecipientAddress.ToDto(),
        Content, Paper, Envelope, Font, Ink, Scent, PostcardCategory, PostcardDesign);

    public static LetterFormViewModel From(LetterDetailsDto d) => new()
    {
        Id = d.Id,
        SenderName = d.SenderName,
        SenderAddress = AddressInput.From(d.SenderAddress),
        RecipientName = d.RecipientName,
        RecipientType = d.RecipientType,
        RecipientDetail = d.RecipientDetail,
        RecipientAddress = AddressInput.From(d.RecipientAddress),
        Content = d.Content,
        Paper = d.Style.Paper,
        Envelope = d.Style.Envelope,
        Font = d.Style.Font,
        Ink = d.Style.Ink,
        Scent = d.Scent,
        PostcardCategory = d.Postcard?.Category,
        PostcardDesign = d.Postcard?.Design,
        PhotoCount = d.Photos.Count
    };
}

public sealed class CheckoutViewModel
{
    public required LetterDetailsDto Letter { get; init; }
    public required QuoteDto Quote { get; init; }
}

public sealed class PendingPaymentViewModel
{
    public required LetterDetailsDto Letter { get; init; }
    public MektupApp.Application.Payments.PaymentStatusDto? Payment { get; init; }
}

public sealed class ShipInput
{
    [Required(ErrorMessage = "Takip kodu zorunludur.")]
    public string TrackingCode { get; set; } = string.Empty;
}
