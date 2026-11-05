using System.ComponentModel.DataAnnotations;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Loyalty;

// MektupPuan: gönderilen her mektup için kazanılan, sonraki ödemelerde 1 puan = 1 ₺ olarak kullanılan puan.

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
    [Display(Name = "Düzeltme")] Adjusted = 3
}

/// <summary>Puan kazanım kuralı. Sistemde tek kayıt; yönetim panelinden değiştirilir.</summary>
public sealed class LoyaltySettings : AggregateRoot
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000002");

    private LoyaltySettings() { } // EF Core

    public bool IsEnabled { get; private set; }
    public RewardType RewardType { get; private set; }
    public decimal Value { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Varsayılan: kapalı, ödenen tutarın %5'i.</summary>
    public static LoyaltySettings CreateDefault() => new()
    {
        Id = SingletonId,
        IsEnabled = false,
        RewardType = RewardType.Percentage,
        Value = 5,
        UpdatedAt = DateTime.UtcNow,
        UpdatedBy = "sistem"
    };

    public void Update(bool isEnabled, RewardType type, decimal value, string updatedBy)
    {
        if (!Enum.IsDefined(type)) throw new DomainException("Geçersiz puan türü.");
        if (type == RewardType.Percentage && value is < 0 or > 100)
            throw new DomainException("Yüzde değeri 0 ile 100 arasında olmalıdır.");
        if (type == RewardType.Fixed && value is < 0 or > 10_000)
            throw new DomainException("Sabit puan 0 ile 10.000 arasında olmalıdır.");

        IsEnabled = isEnabled;
        RewardType = type;
        Value = decimal.Round(value, 2);
        UpdatedBy = Guard.Required(updatedBy, "Güncelleyen", 200);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Tahsil edilen tutara göre kazanılacak puanı hesaplar (puanla ödenen kısım puan kazandırmaz).</summary>
    public decimal CalculateReward(decimal amountCharged)
    {
        if (!IsEnabled) return 0;
        return RewardType switch
        {
            RewardType.Percentage => decimal.Round(Math.Max(0, amountCharged) * Value / 100m, 2, MidpointRounding.AwayFromZero),
            RewardType.Fixed => Value,
            _ => 0
        };
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

    /// <summary>Ödemede puan kullanır. Bakiyeden fazlası kullanılamaz.</summary>
    public PointTransaction? Spend(Guid letterId, decimal points)
    {
        if (points <= 0) return null;
        if (points > Balance)
            throw new DomainException($"Yetersiz puan bakiyesi. Kullanılabilir: {Balance:N2}");
        if (HasTransaction(letterId, PointTransactionType.Spent))
            throw new DomainException("Bu mektup için puan zaten kullanıldı.");

        return Add(PointTransactionType.Spent, -points, letterId, "Mektup ödemesinde kullanıldı.");
    }

    /// <summary>İptal edilen mektupta kullanılan puanı iade eder (bir kez).</summary>
    public PointTransaction? RefundSpent(Guid letterId)
    {
        if (HasTransaction(letterId, PointTransactionType.Refunded)) return null;
        var spent = _transactions.FirstOrDefault(t => t.LetterId == letterId && t.Type == PointTransactionType.Spent);
        if (spent is null) return null;

        return Add(PointTransactionType.Refunded, -spent.Points, letterId, "İptal edilen mektubun puanı iade edildi.");
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
