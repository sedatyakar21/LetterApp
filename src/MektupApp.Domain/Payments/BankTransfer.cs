using System.Numerics;
using System.Text.RegularExpressions;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Payments;

/// <summary>
/// Havale / EFT ile ödeme ayarları (sistemde tek kayıt; yönetim panelinden girilir): açık mı, indirim oranı,
/// paranın gönderileceği banka hesabı ve siparişin ne kadar süre bekleyeceği.
/// </summary>
public sealed partial class BankTransferSettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000005");
    public const string ProviderName = "BankTransfer";
    public const decimal MaxDiscountRate = 0.50m;

    private BankTransferSettings() { } // EF Core

    public bool Enabled { get; private set; }
    /// <summary>Havale / EFT ile ödemede uygulanan indirim (0,05 = %5).</summary>
    public decimal DiscountRate { get; private set; }
    public string BankName { get; private set; } = string.Empty;
    public string AccountHolder { get; private set; } = string.Empty;
    /// <summary>Boşluksuz, büyük harfli TR IBAN (TR + 24 hane).</summary>
    public string Iban { get; private set; } = string.Empty;
    /// <summary>Müşteriye gösterilen ek açıklama (ör. "Açıklamaya sipariş numaranızı yazın").</summary>
    public string? Note { get; private set; }
    /// <summary>Havalenin gelmesi için beklenecek gün; dolunca sipariş taslağa döner.</summary>
    public int ValidDays { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static BankTransferSettings CreateDefault() => new()
    {
        Id = SingletonId, Enabled = false, DiscountRate = 0.05m, ValidDays = 3, UpdatedAt = DateTime.UtcNow, UpdatedBy = "sistem"
    };

    /// <summary>Müşteriye sunulabilir mi: açık ve hesap bilgileri tam.</summary>
    public bool IsAvailable => Enabled && Iban.Length > 0 && AccountHolder.Length > 0 && BankName.Length > 0;

    public void Update(bool enabled, decimal discountRate, string? bankName, string? accountHolder, string? iban,
        string? note, int validDays, string updatedBy)
    {
        if (discountRate < 0 || discountRate > MaxDiscountRate)
            throw new DomainException($"Havale / EFT indirimi %0 ile %{MaxDiscountRate * 100:0} arasında olmalıdır.");
        if (validDays is < 1 or > 30) throw new DomainException("Bekleme süresi 1 ile 30 gün arasında olmalıdır.");
        var normalizedIban = NormalizeIban(iban);
        var bank = Guard.Optional(bankName, "Banka adı", 100) ?? string.Empty;
        var holder = Guard.Optional(accountHolder, "Hesap sahibi", 150) ?? string.Empty;
        if (enabled && (normalizedIban.Length == 0 || bank.Length == 0 || holder.Length == 0))
            throw new DomainException("Havale / EFT'yi açmak için banka adı, hesap sahibi ve IBAN girilmelidir.");

        Enabled = enabled;
        DiscountRate = decimal.Round(discountRate, 4);
        BankName = bank;
        AccountHolder = holder;
        Iban = normalizedIban;
        Note = Guard.Optional(note, "Açıklama", 500);
        ValidDays = validDays;
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>İndirim tutarı (kuruşa yuvarlanır).</summary>
    public decimal DiscountFor(decimal amount) => amount <= 0 ? 0 : decimal.Round(amount * DiscountRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>"TR12 3456 ..." biçiminde, okunaklı IBAN.</summary>
    public string IbanDisplay => string.Join(' ', Enumerable.Range(0, (Iban.Length + 3) / 4).Select(i => Iban.Substring(i * 4, Math.Min(4, Iban.Length - i * 4))));

    /// <summary>Boşlukları atar, büyük harfe çevirir; TR IBAN biçimi ve kontrol hanesi (mod 97) doğrulanır. Boşsa "".</summary>
    public static string NormalizeIban(string? iban)
    {
        var v = new string((iban ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (v.Length == 0) return v;
        if (!TrIban().IsMatch(v)) throw new DomainException("IBAN TR ile başlamalı ve 26 karakter olmalıdır (TR + 24 rakam).");
        // ISO 13616: ilk 4 karakter sona alınır, harfler sayıya çevrilir (A=10 … Z=35), sonuç mod 97 = 1 olmalıdır.
        var rearranged = v[4..] + v[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));
        if (BigInteger.Parse(digits) % 97 != 1) throw new DomainException("IBAN geçersiz (kontrol hanesi tutmuyor). Lütfen kontrol edin.");
        return v;
    }

    [GeneratedRegex(@"^TR\d{24}$")]
    private static partial Regex TrIban();
}

public interface IBankTransferSettingsRepository
{
    Task<BankTransferSettings?> GetAsync(CancellationToken ct = default);
    void Add(BankTransferSettings settings);
}
