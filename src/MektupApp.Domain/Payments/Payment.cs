using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Payments;

public enum PaymentStatus
{
    [Display(Name = "Bekleniyor")] Pending = 0,
    [Display(Name = "Başarılı")] Succeeded = 1,
    [Display(Name = "Başarısız")] Failed = 2,
    [Display(Name = "Süresi doldu")] Expired = 3
}

public enum RefundStatus
{
    [Display(Name = "—")] None = 0,
    [Display(Name = "İade edildi")] Refunded = 1,
    [Display(Name = "İade başarısız")] Failed = 2
}

/// <summary>
/// Ödeme denemesi (aggregate root). Bir mektup için birden fazla deneme olabilir; en fazla biri başarılı olur.
/// Tutar, kullanılan puan ve kazanılacak puanlar deneme başlarken sabitlenir; PayTR'nin
/// bildirimi geldiğinde mektuba bu değerler aktarılır.
/// </summary>
public sealed class Payment : AggregateRoot
{
    private Payment() { } // EF Core

    /// <summary>Ödeme sağlayıcısına giden sipariş no (PayTR: merchant_oid; sadece harf ve rakam, en fazla 64).</summary>
    public string MerchantOid { get; private set; } = string.Empty;
    public Guid LetterId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public PaymentMethod Method { get; private set; }

    /// <summary>Karttan tahsil edilecek tutar (puan indirimi düşülmüş).</summary>
    public decimal Amount { get; private set; }
    public decimal PointsUsed { get; private set; }
    public decimal PointsDiscount { get; private set; }
    public decimal PointsToEarn { get; private set; }
    public Guid? ReferrerCustomerId { get; private set; }
    public decimal ReferralPointsToEarn { get; private set; }

    public PaymentStatus Status { get; private set; }
    /// <summary>Ödeme formunun adresi (PayTR iFrame). Müşteri sayfayı kapatırsa geri dönebilsin diye saklanır.</summary>
    public string? FormUrl { get; private set; }
    /// <summary>Sağlayıcının bildirdiği tahsil edilen tutar (taksit farkıyla Amount'tan büyük olabilir).</summary>
    public decimal? PaidTotal { get; private set; }
    public string? FailureReason { get; private set; }
    public bool IsTest { get; private set; }

    public RefundStatus RefundStatus { get; private set; }
    public decimal? RefundedAmount { get; private set; }
    public string? RefundError { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static Payment Start(Guid letterId, Guid customerId, string provider, PaymentMethod method,
        decimal amount, decimal pointsUsed, decimal pointsDiscount, decimal pointsToEarn,
        Guid? referrerCustomerId, decimal referralPointsToEarn)
    {
        if (amount < 0 || pointsUsed < 0 || pointsDiscount < 0 || pointsToEarn < 0 || referralPointsToEarn < 0)
            throw new DomainException("Ödeme tutarları negatif olamaz.");

        return new Payment
        {
            MerchantOid = NewMerchantOid(),
            LetterId = letterId,
            CustomerId = customerId,
            Provider = Guard.Required(provider, "Ödeme sağlayıcısı", 20),
            Method = method,
            Amount = decimal.Round(amount, 2),
            PointsUsed = pointsUsed,
            PointsDiscount = pointsDiscount,
            PointsToEarn = pointsToEarn,
            ReferrerCustomerId = referrerCustomerId,
            ReferralPointsToEarn = referrerCustomerId is null ? 0 : referralPointsToEarn,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AttachForm(string formUrl) => FormUrl = Guard.Required(formUrl, "Ödeme formu", 500);

    public bool IsPending => Status == PaymentStatus.Pending;

    /// <summary>Sağlayıcı ödemenin alındığını bildirdi. Eksik tahsilat kabul edilmez.</summary>
    public void Succeed(decimal paidTotal, bool isTest)
    {
        EnsurePending();
        if (paidTotal < Amount)
            throw new DomainException($"Tahsil edilen tutar ({paidTotal:N2}) beklenenden ({Amount:N2}) az.");
        Status = PaymentStatus.Succeeded;
        PaidTotal = paidTotal;
        IsTest = isTest;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deneme kapandıktan (süresi dolduktan / mektup başka yoldan ödendikten) sonra gelen başarılı ödeme.
    /// Para alınmıştır ama mektuba işlenmez; iade edilmelidir.
    /// </summary>
    public void RecordLateSuccess(decimal paidTotal, bool isTest)
    {
        if (Status == PaymentStatus.Succeeded) return;
        Status = PaymentStatus.Succeeded;
        PaidTotal = paidTotal;
        IsTest = isTest;
        FailureReason = "Ödeme, deneme kapandıktan sonra onaylandı; mektuba işlenmedi ve iade edilmeli.";
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        EnsurePending();
        Status = PaymentStatus.Failed;
        FailureReason = Guard.Optional(reason, "Hata nedeni", 300) ?? "Ödeme onaylanmadı.";
        CompletedAt = DateTime.UtcNow;
    }

    public void Expire(string reason = "Ödeme süresi doldu.")
    {
        EnsurePending();
        Status = PaymentStatus.Expired;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    public decimal RefundableAmount => Status == PaymentStatus.Succeeded && RefundStatus != RefundStatus.Refunded ? Amount : 0;

    public void MarkRefunded(decimal amount)
    {
        if (Status != PaymentStatus.Succeeded) throw new DomainException("Sadece başarılı ödemeler iade edilebilir.");
        RefundStatus = RefundStatus.Refunded;
        RefundedAmount = amount;
        RefundError = null;
    }

    public void MarkRefundFailed(string error)
    {
        RefundStatus = RefundStatus.Failed;
        RefundError = Guard.Optional(error, "İade hatası", 300) ?? "İade yapılamadı.";
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Bu ödeme denemesi zaten sonuçlandı.");
    }

    /// <summary>"MK" + tarih + rastgele 8 karakter: benzersiz, sadece harf/rakam (PayTR kuralı).</summary>
    private static string NewMerchantOid() =>
        $"MK{DateTime.UtcNow:yyyyMMddHHmmss}{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
}

public interface IPaymentRepository
{
    Task<Payment?> GetByMerchantOidAsync(string merchantOid, CancellationToken ct = default);
    Task<Payment?> GetSucceededForLetterAsync(Guid letterId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> ListForLetterAsync(Guid letterId, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> ListPendingOlderThanAsync(DateTime createdBefore, int maxCount, CancellationToken ct = default);
    void Add(Payment payment);
}
