using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Loyalty;

// MektupPuan: gönderilen her mektup için kazanılan, sonraki ödemelerde TL'ye çevrilerek kullanılan puan.
// 1 puanın TL karşılığı yönetim panelinden ayarlanır (LoyaltySettings.PointValue).

/// <summary>Ödemede puan kullanımının sonucu: harcanan puan ve karşılığı olan TL indirim.</summary>
public readonly record struct PointRedemption(decimal Points, decimal Discount);

public enum RewardType
{
    [Display(Name = "Ödenen tutarın yüzdesi")] Percentage = 0,
    [Display(Name = "Mektup başına sabit puan")] Fixed = 1
}

public enum PointTransactionType
{
    [Display(Name = "Kazanıldı")] Earned = 0,
    [Display(Name = "Kullanıldı")] Spent = 1,
    [Display(Name = "İade edildi")] Refunded = 2,
    [Display(Name = "Düzeltme")] Adjusted = 3,
    [Display(Name = "Referans kazancı")] ReferralEarned = 4
}

/// <summary>Puan kazanım kuralı. Sistemde tek kayıt; yönetim panelinden değiştirilir.</summary>
public sealed class LoyaltySettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000002");

    private LoyaltySettings() { } // EF Core

    // Mektubu gönderen üyenin kendi kazancı
    public bool IsEnabled { get; private set; }
    public RewardType RewardType { get; private set; }
    public decimal Value { get; private set; }

    // Referans: davet edilen üyenin her mektubunda, davet eden üyenin kazancı
    public bool ReferralEnabled { get; private set; }
    public RewardType ReferralRewardType { get; private set; }
    public decimal ReferralValue { get; private set; }

    /// <summary>1 puanın TL karşılığı (ör. 0,50 → 1 puan = 0,50 ₺). Sadece harcamada kullanılır.</summary>
    public decimal PointValue { get; private set; } = 1m;

    public const decimal MinPointValue = 0.01m;
    public const decimal MaxPointValue = 1000m;

    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Varsayılan: kapalı; kendi kazancı %5, referans kazancı %3, 1 puan = 1 ₺.</summary>
    public static LoyaltySettings CreateDefault() => new()
    {
        Id = SingletonId,
        IsEnabled = false,
        RewardType = RewardType.Percentage,
        Value = 5,
        ReferralEnabled = false,
        ReferralRewardType = RewardType.Percentage,
        ReferralValue = 3,
        PointValue = 1m,
        UpdatedAt = DateTime.UtcNow,
        UpdatedBy = "sistem"
    };

    public void Update(bool isEnabled, RewardType type, decimal value, string updatedBy)
    {
        Validate(type, value);
        IsEnabled = isEnabled;
        RewardType = type;
        Value = decimal.Round(value, 2);
        Touch(updatedBy);
    }

    public void UpdateReferral(bool isEnabled, RewardType type, decimal value, string updatedBy)
    {
        Validate(type, value);
        ReferralEnabled = isEnabled;
        ReferralRewardType = type;
        ReferralValue = decimal.Round(value, 2);
        Touch(updatedBy);
    }

    /// <summary>1 puanın TL karşılığını değiştirir. Mevcut bakiyelerin TL değeri de buna göre değişir.</summary>
    public void UpdatePointValue(decimal pointValue, string updatedBy)
    {
        if (pointValue is < MinPointValue or > MaxPointValue)
            throw new DomainException($"1 puanın değeri {MinPointValue:N2} ₺ ile {MaxPointValue:N0} ₺ arasında olmalıdır.");
        PointValue = decimal.Round(pointValue, 4);
        Touch(updatedBy);
    }

    /// <summary>Puan miktarının TL karşılığı.</summary>
    public decimal ToMoney(decimal points) =>
        decimal.Round(points * PointValue, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Ödemede kullanılacak puanı hesaplar: bakiye yettiği kadar, toplam tutarı aşmayacak şekilde.
    /// Tutarın tamamını karşılamak için gereken puan yukarı yuvarlanır; indirim tutarı aşamaz.
    /// </summary>
    public PointRedemption Redeem(decimal total, decimal balance)
    {
        if (total <= 0 || balance <= 0) return new PointRedemption(0, 0);

        var pointsForTotal = Math.Ceiling(total / PointValue * 100m) / 100m;
        var points = Math.Min(balance, pointsForTotal);
        var discount = Math.Min(total, ToMoney(points));
        return new PointRedemption(points, discount);
    }

    /// <summary>Tahsil edilen tutara göre gönderenin kazanacağı puan (puanla ödenen kısım puan kazandırmaz).</summary>
    public decimal CalculateReward(decimal amountCharged) =>
        IsEnabled ? Calculate(RewardType, Value, amountCharged) : 0;

    /// <summary>Davet edilen üyenin bu ödemesi için davet eden üyenin kazanacağı puan.</summary>
    public decimal CalculateReferralReward(decimal amountCharged) =>
        ReferralEnabled ? Calculate(ReferralRewardType, ReferralValue, amountCharged) : 0;

    private static decimal Calculate(RewardType type, decimal value, decimal amountCharged) => type switch
    {
        RewardType.Percentage => decimal.Round(Math.Max(0, amountCharged) * value / 100m, 2, MidpointRounding.AwayFromZero),
        RewardType.Fixed => value,
        _ => 0
    };

    private static void Validate(RewardType type, decimal value)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Geçersiz puan türü.");
        if (type == RewardType.Percentage && value is < 0 or > 100)
            throw new DomainException("Yüzde değeri 0 ile 100 arasında olmalıdır.");
        if (type == RewardType.Fixed && value is < 0 or > 10_000)
            throw new DomainException("Sabit puan 0 ile 10.000 arasında olmalıdır.");
    }

    private void Touch(string updatedBy)
    {
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Müşterinin puan hesabı (aggregate root). Bakiye yalnızca hareketlerle değişir;
/// her hareket kayıt altındadır ve bakiye hiçbir zaman eksiye düşmez.
/// </summary>
public sealed class LoyaltyAccount : AggregateRoot
{
    private readonly List<PointTransaction> _transactions = [];

    private LoyaltyAccount() { } // EF Core

    public Guid CustomerId { get; private set; }
    public decimal Balance { get; private set; }
    public decimal TotalEarned { get; private set; }
    public IReadOnlyCollection<PointTransaction> Transactions => _transactions.AsReadOnly();

    public static LoyaltyAccount Open(Guid customerId)
    {
        if (customerId == Guid.Empty) throw new DomainException("Müşteri bilgisi zorunludur.");
        return new LoyaltyAccount { CustomerId = customerId };
    }

    /// <summary>Gönderilen mektup için puan ekler. Aynı mektup için ikinci kez puan verilmez.</summary>
    public PointTransaction? Earn(Guid letterId, decimal points)
    {
        if (points <= 0) return null;
        if (HasTransaction(letterId, PointTransactionType.Earned)) return null;

        TotalEarned += points;
        return Add(PointTransactionType.Earned, points, letterId, "Gönderilen mektup için kazanıldı.");
    }

    /// <summary>Davet ettiği üyenin gönderilen mektubu için puan ekler. Aynı mektup için bir kez.</summary>
    public PointTransaction? EarnReferral(Guid letterId, decimal points, string referredName)
    {
        if (points <= 0) return null;
        if (HasTransaction(letterId, PointTransactionType.ReferralEarned)) return null;

        TotalEarned += points;
        return Add(PointTransactionType.ReferralEarned, points, letterId,
            $"Davet ettiğiniz {Guard.Required(referredName, "Ad", 100)} bir mektup gönderdi.");
    }

    /// <summary>
    /// Ödemede puan kullanır (kart ödemesi başlarken puan ayrılır). Bakiyeden fazlası kullanılamaz.
    /// Aynı mektup için iade edilmemiş bir harcama varken yenisi yapılamaz; başarısız ödemeden
    /// sonra puan iade edildiyse tekrar kullanılabilir.
    /// </summary>
    public PointTransaction? Spend(Guid letterId, decimal points)
    {
        if (points <= 0) return null;
        if (points > Balance)
            throw new DomainException($"Yetersiz puan bakiyesi. Kullanılabilir: {Balance:N2}");
        if (OpenSpend(letterId) is not null)
            throw new DomainException("Bu mektup için puan zaten kullanıldı.");

        return Add(PointTransactionType.Spent, -points, letterId, "Mektup ödemesinde kullanıldı.");
    }

    /// <summary>
    /// Mektup için iade edilmemiş son puan harcamasını geri yükler (iptal veya başarısız ödeme).
    /// İade edilecek harcama yoksa hiçbir şey yapmaz; bu sayede tekrar çağrılması güvenlidir.
    /// </summary>
    public PointTransaction? RefundSpent(Guid letterId, string note = "İptal edilen mektubun puanı iade edildi.")
    {
        var spent = OpenSpend(letterId);
        return spent is null ? null : Add(PointTransactionType.Refunded, -spent.Points, letterId, note);
    }

    /// <summary>İade edilmemiş son harcama (harcama sayısı iade sayısından fazlaysa).</summary>
    private PointTransaction? OpenSpend(Guid letterId)
    {
        var spends = _transactions.Where(t => t.LetterId == letterId && t.Type == PointTransactionType.Spent).ToList();
        var refunds = _transactions.Count(t => t.LetterId == letterId && t.Type == PointTransactionType.Refunded);
        return spends.Count > refunds ? spends[^1] : null;
    }

    /// <summary>Yöneticinin elle yaptığı düzeltme (+ ekler, − düşer).</summary>
    public PointTransaction Adjust(decimal points, string reason)
    {
        if (points == 0) throw new DomainException("Düzeltme tutarı sıfır olamaz.");
        if (Balance + points < 0)
            throw new DomainException($"Bakiye eksiye düşemez. Mevcut bakiye: {Balance:N2}");

        return Add(PointTransactionType.Adjusted, points, null, Guard.Required(reason, "Açıklama", 200));
    }

    private bool HasTransaction(Guid letterId, PointTransactionType type) =>
        _transactions.Any(t => t.LetterId == letterId && t.Type == type);

    private PointTransaction Add(PointTransactionType type, decimal points, Guid? letterId, string note)
    {
        points = decimal.Round(points, 2);
        var tx = new PointTransaction(Id, type, points, letterId, note);
        _transactions.Add(tx);
        Balance += points;
        return tx;
    }
}

/// <summary>Puan hareketi. Points: + bakiyeye ekler, − bakiyeden düşer.</summary>
public sealed class PointTransaction : Entity
{
    private PointTransaction() { } // EF Core

    internal PointTransaction(Guid accountId, PointTransactionType type, decimal points, Guid? letterId, string note)
    {
        AccountId = accountId;
        Type = type;
        Points = points;
        LetterId = letterId;
        Note = note;
        At = DateTime.UtcNow;
    }

    public Guid AccountId { get; private set; }
    public PointTransactionType Type { get; private set; }
    public decimal Points { get; private set; }
    public Guid? LetterId { get; private set; }
    public string Note { get; private set; } = string.Empty;
    public DateTime At { get; private set; }
}

public interface ILoyaltyAccountRepository
{
    Task<LoyaltyAccount?> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    void Add(LoyaltyAccount account);
}

public interface ILoyaltySettingsRepository
{
    Task<LoyaltySettings?> GetAsync(CancellationToken ct = default);
    void Add(LoyaltySettings settings);
}
