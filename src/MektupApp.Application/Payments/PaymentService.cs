using MektupApp.Application.Abstractions;
using MektupApp.Application.Loyalty;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;
using MektupApp.Domain.Payments;

namespace MektupApp.Application.Payments;

/// <param name="ResultUrlFor">Ödeme sonrası müşterinin döneceği sayfanın adresi (sipariş no'ya göre).</param>
public sealed record StartPaymentCommand(
    Guid LetterId,
    Guid CustomerId,
    PaymentMethod Method,
    bool UsePoints,
    string CustomerIp,
    Func<string, string> ResultUrlFor);

/// <param name="Completed">true: ödeme tamamlandı (tamamı puanla ödendi), forma yönlendirme yok.</param>
public sealed record StartPaymentResult(bool Completed, string MerchantOid, string? FormUrl);

public sealed record PaymentStatusDto(
    string MerchantOid,
    Guid LetterId,
    PaymentStatus Status,
    decimal Amount,
    string? FormUrl,
    string? FailureReason,
    DateTime CreatedAt);

public sealed record PaymentRowDto(
    string MerchantOid,
    string Provider,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    decimal? PaidTotal,
    decimal PointsUsed,
    string? FailureReason,
    bool IsTest,
    RefundStatus RefundStatus,
    decimal? RefundedAmount,
    string? RefundError,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public enum PaymentCompletion { Paid, Failed, AlreadyProcessed, LateSuccessRefunded, LateSuccessRefundFailed, Unknown }

/// <summary>
/// Ödeme use case'leri. Akış:
///  1) StartAsync: tutar sabitlenir, puan ayrılır, mektup kilitlenir, sağlayıcı formu hazırlanır.
///  2) CompleteAsync: sağlayıcının imzalı bildirimi gelir → mektup ödenir veya taslağa döner + puan iade.
///  3) AbandonAsync / ExpireStaleAsync: müşteri vazgeçti veya süre doldu → taslağa dön + puan iade.
/// </summary>
public sealed class PaymentService(
    ILetterRepository letters,
    ICustomerRepository customers,
    IPaymentRepository payments,
    ILoyaltyAccountRepository loyaltyAccounts,
    LoyaltyService loyalty,
    CheckoutCalculator checkout,
    IPaymentProvider provider,
    IUnitOfWork unitOfWork)
{
    public const string PointsOnlyProvider = "MektupPuan";

    public string ProviderName => provider.Name;
    public bool ProviderIsSimulation => provider.IsSimulation;

    public async Task<StartPaymentResult> StartAsync(StartPaymentCommand cmd, CancellationToken ct = default)
    {
        var letter = await LoadOwnedLetterAsync(cmd.LetterId, cmd.CustomerId, ct);
        letter.EnsureReadyForCheckout();
        if (cmd.Method == PaymentMethod.Mobile && !provider.SupportsMobilePayment)
            throw new AppException("Mobil ödeme şu an kullanılamıyor. Lütfen kart ile ödeyin.");

        var customer = await customers.GetAsync(cmd.CustomerId, ct) ?? throw new AppException("Müşteri bulunamadı.");
        var (quote, account) = await checkout.CalculateAsync(letter, cmd.Method, cmd.UsePoints, ct);
        var (referrerId, referralPoints) = await ReferralRewardAsync(customer, quote.AmountToPay, ct);

        var pointsOnly = quote.AmountToPay == 0;
        var payment = Payment.Start(letter.Id, customer.Id, pointsOnly ? PointsOnlyProvider : provider.Name, cmd.Method,
            quote.AmountToPay, quote.PointsUsed, quote.PointsDiscount, quote.PointsToEarn, referrerId, referralPoints);
        payments.Add(payment);

        // Puan, ödeme başlarken ayrılır: aynı puan iki ayrı ödemede kullanılamaz.
        // Ödeme başarısız olur veya süresi dolarsa iade edilir.
        if (quote.PointsUsed > 0)
            (account ?? throw new AppException("Puan hesabı bulunamadı.")).Spend(letter.Id, quote.PointsUsed);

        if (pointsOnly)
        {
            payment.Succeed(0, isTest: false);
            ApplyToLetter(letter, payment);
            await unitOfWork.SaveChangesAsync(ct);
            return new StartPaymentResult(true, payment.MerchantOid, null);
        }

        // Sağlayıcıya gitmeden önce kilitle ve kaydet: bildirim çok hızlı gelse bile kaydı bulabilsin.
        letter.BeginPayment(payment.MerchantOid);
        await unitOfWork.SaveChangesAsync(ct);

        var start = await provider.StartAsync(new PaymentStartRequest(
            payment.MerchantOid,
            payment.Amount,
            new PaymentBuyer(customer.FullName, customer.Email.Value, "0" + customer.Phone.Value,
                letter.SenderAddress.ToString(), cmd.CustomerIp),
            [new PaymentBasketItem($"Mektup gönderimi - {letter.Recipient.FullName}", payment.Amount, 1)],
            cmd.ResultUrlFor(payment.MerchantOid),
            cmd.ResultUrlFor(payment.MerchantOid)), ct);

        if (!start.Succeeded || start.FormUrl is null)
        {
            await CloseUnpaidAsync(payment, letter, p => p.Fail(start.Error ?? "Ödeme formu açılamadı."),
                "Ödeme başlatılamadı.", ct);
            throw new AppException($"Ödeme başlatılamadı: {start.Error ?? "bilinmeyen hata"}. Lütfen tekrar deneyin.");
        }

        payment.AttachForm(start.FormUrl);
        await unitOfWork.SaveChangesAsync(ct);
        return new StartPaymentResult(false, payment.MerchantOid, start.FormUrl);
    }

    /// <summary>
    /// Sağlayıcının (imzası doğrulanmış) bildirimini işler. Aynı bildirim birden fazla gelebilir;
    /// ilki işlenir, sonrakiler "zaten işlendi" döner.
    /// </summary>
    public async Task<PaymentCompletion> CompleteAsync(PaymentNotification n, CancellationToken ct = default)
    {
        var payment = await payments.GetByMerchantOidAsync(n.MerchantOid, ct);
        if (payment is null) return PaymentCompletion.Unknown;

        var letter = await letters.GetAsync(payment.LetterId, ct);

        if (!payment.IsPending)
        {
            // Deneme kapandıktan sonra (vazgeçildi / süre doldu) para çekildiyse otomatik iade edilir.
            if (n.Success && payment.Status != PaymentStatus.Succeeded)
                return await RefundLateSuccessAsync(payment, n, ct);
            return PaymentCompletion.AlreadyProcessed;
        }

        if (!n.Success)
        {
            await CloseUnpaidAsync(payment, letter, p => p.Fail(n.FailureReason ?? "Ödeme onaylanmadı."),
                $"Ödeme başarısız: {n.FailureReason ?? "banka onay vermedi"}", ct);
            return PaymentCompletion.Failed;
        }

        if (letter is null || !letter.IsAwaitingPayment(payment.MerchantOid))
            return await RefundLateSuccessAsync(payment, n, ct);

        payment.Succeed(n.TotalAmount, n.IsTest);
        ApplyToLetter(letter, payment);
        await unitOfWork.SaveChangesAsync(ct);
        return PaymentCompletion.Paid;
    }

    /// <summary>Müşteri ödeme sayfasından vazgeçti: mektup tekrar düzenlenebilir, ayrılan puan iade edilir.</summary>
    public async Task AbandonAsync(Guid letterId, Guid customerId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedLetterAsync(letterId, customerId, ct);
        if (letter.Status != LetterStatus.AwaitingPayment || letter.PaymentReference is null) return;

        var payment = await payments.GetByMerchantOidAsync(letter.PaymentReference, ct);
        if (payment is { IsPending: true })
            await CloseUnpaidAsync(payment, letter, p => p.Expire("Müşteri ödemeden vazgeçti."), "Ödemeden vazgeçildi.", ct);
        else
        {
            letter.AbandonPayment("Ödemeden vazgeçildi.");
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>Arka plan görevi: sonucu gelmeyen eski ödeme denemelerini kapatır.</summary>
    public async Task<int> ExpireStaleAsync(TimeSpan maxAge, int maxCount, CancellationToken ct = default)
    {
        var stale = await payments.ListPendingOlderThanAsync(DateTime.UtcNow - maxAge, maxCount, ct);
        foreach (var payment in stale)
        {
            var letter = await letters.GetAsync(payment.LetterId, ct);
            await CloseUnpaidAsync(payment, letter, p => p.Expire(), "Ödeme süresi doldu.", ct);
        }
        return stale.Count;
    }

    public async Task<PaymentStatusDto?> GetStatusAsync(string merchantOid, Guid customerId, CancellationToken ct = default)
    {
        var p = await payments.GetByMerchantOidAsync(merchantOid, ct);
        return p is null || p.CustomerId != customerId ? null : ToStatus(p);
    }

    /// <summary>Mektubun bekleyen ödemesi (müşteri ödeme sayfasını kapattıysa geri dönebilsin).</summary>
    public async Task<PaymentStatusDto?> GetPendingForLetterAsync(Guid letterId, Guid customerId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedLetterAsync(letterId, customerId, ct);
        if (letter.Status != LetterStatus.AwaitingPayment || letter.PaymentReference is null) return null;
        var p = await payments.GetByMerchantOidAsync(letter.PaymentReference, ct);
        return p is { IsPending: true } ? ToStatus(p) : null;
    }

    public async Task<IReadOnlyList<PaymentRowDto>> ListForLetterAsync(Guid letterId, CancellationToken ct = default) =>
        (await payments.ListForLetterAsync(letterId, ct))
            .Select(p => new PaymentRowDto(p.MerchantOid, p.Provider, p.Method, p.Status, p.Amount, p.PaidTotal, p.PointsUsed,
                p.FailureReason, p.IsTest, p.RefundStatus, p.RefundedAmount, p.RefundError, p.CreatedAt, p.CompletedAt))
            .ToList();

    // ---------------- yardımcılar ----------------

    private static void ApplyToLetter(Letter letter, Payment p) =>
        letter.MarkPaid(new Money(p.Amount), p.Method, p.MerchantOid, p.PointsUsed, p.PointsToEarn,
            p.ReferrerCustomerId, p.ReferralPointsToEarn, pointsDiscount: p.PointsDiscount);

    /// <summary>Ödenmeden kapanan deneme: durumu işaretle, mektubu taslağa döndür, ayrılan puanı iade et.</summary>
    private async Task CloseUnpaidAsync(Payment payment, Letter? letter, Action<Payment> close, string letterNote, CancellationToken ct)
    {
        close(payment);
        if (letter is not null && letter.IsAwaitingPayment(payment.MerchantOid))
            letter.AbandonPayment(letterNote);

        if (payment.PointsUsed > 0 && await loyaltyAccounts.GetByCustomerAsync(payment.CustomerId, ct) is { } account)
            account.RefundSpent(payment.LetterId, "Ödeme tamamlanmadı, ayrılan puan iade edildi.");

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<PaymentCompletion> RefundLateSuccessAsync(Payment payment, PaymentNotification n, CancellationToken ct)
    {
        payment.RecordLateSuccess(n.TotalAmount, n.IsTest);
        await unitOfWork.SaveChangesAsync(ct);

        var refund = await provider.RefundAsync(payment.MerchantOid, payment.RefundableAmount, ct);
        if (refund.Succeeded) payment.MarkRefunded(payment.RefundableAmount);
        else payment.MarkRefundFailed(refund.Error ?? "İade yapılamadı.");
        await unitOfWork.SaveChangesAsync(ct);

        return refund.Succeeded ? PaymentCompletion.LateSuccessRefunded : PaymentCompletion.LateSuccessRefundFailed;
    }

    private async Task<(Guid? ReferrerId, decimal Points)> ReferralRewardAsync(Customer customer, decimal amountCharged, CancellationToken ct)
    {
        if (customer.ReferredByCustomerId is not { } refId || await customers.GetAsync(refId, ct) is not { IsActive: true })
            return (null, 0);
        return (refId, (await loyalty.GetSettingsAsync(ct)).CalculateReferralReward(amountCharged));
    }

    private async Task<Letter> LoadOwnedLetterAsync(Guid letterId, Guid customerId, CancellationToken ct)
    {
        var letter = await letters.GetAsync(letterId, ct);
        if (letter is null || letter.IsDeleted || letter.CustomerId != customerId)
            throw new AppException("Mektup bulunamadı.");
        return letter;
    }

    private static PaymentStatusDto ToStatus(Payment p) =>
        new(p.MerchantOid, p.LetterId, p.Status, p.Amount, p.FormUrl, p.FailureReason, p.CreatedAt);
}

/// <summary>Ödemesi alınmış mektup iptal edilince, ödeme sağlayıcısında iade başlatır.</summary>
public sealed class PaymentRefundHandler(IPaymentRepository payments, IPaymentProvider provider, IUnitOfWork unitOfWork)
    : IDomainEventHandler<LetterCancelledAfterPayment>
{
    public async Task HandleAsync(LetterCancelledAfterPayment e, CancellationToken ct)
    {
        var payment = await payments.GetSucceededForLetterAsync(e.LetterId, ct);
        if (payment is null || payment.RefundableAmount <= 0 || payment.Provider != provider.Name) return;

        var refund = await provider.RefundAsync(payment.MerchantOid, payment.RefundableAmount, ct);
        if (refund.Succeeded) payment.MarkRefunded(payment.RefundableAmount);
        else payment.MarkRefundFailed(refund.Error ?? "İade yapılamadı.");
        await unitOfWork.SaveChangesAsync(ct);
    }
}
