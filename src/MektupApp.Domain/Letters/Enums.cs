using System.ComponentModel.DataAnnotations;

namespace MektupApp.Domain.Letters;

public enum LetterStatus
{
    [Display(Name = "Taslak")] Draft = 0,
    [Display(Name = "Ödendi")] Paid = 1,
    [Display(Name = "Basımda")] InProduction = 2,
    [Display(Name = "Kargoda")] Shipped = 3,
    [Display(Name = "Teslim Edildi")] Delivered = 4,
    [Display(Name = "İptal Edildi")] Cancelled = 5
}

public enum RecipientType
{
    [Display(Name = "Sevgili / Aile / Arkadaş")] Standard = 0,
    [Display(Name = "Asker")] Soldier = 1,
    [Display(Name = "Cezaevi")] Inmate = 2
}

public enum PaperColor
{
    [Display(Name = "Beyaz")] White = 0,
    [Display(Name = "Krem")] Cream = 1,
    [Display(Name = "Pembe")] Pink = 2,
    [Display(Name = "Mavi")] Blue = 3,
    [Display(Name = "Yeşil")] Green = 4
}

public enum EnvelopeColor
{
    [Display(Name = "Beyaz")] White = 0,
    [Display(Name = "Kraft")] Kraft = 1,
    [Display(Name = "Kırmızı")] Red = 2,
    [Display(Name = "Lacivert")] Navy = 3,
    [Display(Name = "Pembe")] Pink = 4
}

public enum LetterFont
{
    [Display(Name = "Klasik")] Serif = 0,
    [Display(Name = "Modern")] Sans = 1,
    [Display(Name = "El Yazısı")] Handwriting = 2
}

public enum InkColor
{
    [Display(Name = "Siyah")] Black = 0,
    [Display(Name = "Lacivert")] Navy = 1,
    [Display(Name = "Bordo")] Burgundy = 2
}

public enum Scent
{
    [Display(Name = "Kokusuz")] None = 0,
    [Display(Name = "Manolya")] Magnolia = 1,
    [Display(Name = "Lavanta")] Lavender = 2,
    [Display(Name = "Bahar Çiçekleri")] SpringFlowers = 3
}

public enum PostcardCategory
{
    [Display(Name = "Aşk")] Love = 1,
    [Display(Name = "Doğa")] Nature = 2,
    [Display(Name = "Şehirler")] Cities = 3,
    [Display(Name = "Özel Gün")] Celebration = 4,
    [Display(Name = "Motivasyon")] Motivation = 5
}

public enum PaymentMethod
{
    [Display(Name = "Kredi / Banka Kartı")] Card = 0,
    [Display(Name = "Mobil Ödeme")] Mobile = 1
}
