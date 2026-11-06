using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;
using MektupApp.Domain.Payments;

namespace MektupApp.Domain.Tests;

public class PaymentTests
{
    private static Letter Draft()
    {
        var address = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", address, new Recipient("Ayşe", RecipientType.Standard, null, address));
        letter.WriteContent("Merhaba");
        return letter;
    }

    private static Payment Start(Letter l, decimal amount = 290) =>
        Payment.Start(l.Id, l.CustomerId, "PayTR", PaymentMethod.Card, amount, 0, 0, 29, null, 0);

    [Fact]
    public void Merchant_oid_is_alphanumeric_and_unique()
    {
        var l = Draft();
        var a = Start(l);
        var b = Start(l);
        Assert.Matches("^[A-Za-z0-9]{1,64}$", a.MerchantOid);
        Assert.NotEqual(a.MerchantOid, b.MerchantOid);
    }

    [Fact]
    public void Letter_is_locked_while_payment_is_pending_and_paid_by_matching_payment()
    {
        var l = Draft();
        var p = Start(l);
        l.BeginPayment(p.MerchantOid);

        Assert.Equal(LetterStatus.AwaitingPayment, l.Status);
        Assert.Throws<DomainException>(() => l.WriteContent("değişti"));
        Assert.Throws<DomainException>(() => l.MarkPaid(new Money(290), PaymentMethod.Card, "BASKA-ODEME"));

        p.Succeed(290, isTest: true);
        l.MarkPaid(new Money(p.Amount), p.Method, p.MerchantOid, pointsToEarn: p.PointsToEarn);
        Assert.Equal(LetterStatus.Paid, l.Status);
        Assert.Equal(p.MerchantOid, l.PaymentReference);
    }

    [Fact]
    public void Failed_payment_returns_letter_to_draft()
    {
        var l = Draft();
        var p = Start(l);
        l.BeginPayment(p.MerchantOid);

        p.Fail("Yetersiz bakiye");
        l.AbandonPayment("Ödeme başarısız: Yetersiz bakiye");

        Assert.Equal(PaymentStatus.Failed, p.Status);
        Assert.Equal(LetterStatus.Draft, l.Status);
        l.WriteContent("tekrar düzenlenebilir");
    }

    [Fact]
    public void Payment_result_is_applied_only_once_and_short_payment_rejected()
    {
        var p = Start(Draft());
        Assert.Throws<DomainException>(() => p.Succeed(100, false));
        p.Succeed(305.50m, false); // taksit farkıyla fazla tahsilat kabul
        Assert.Throws<DomainException>(() => p.Fail("geç gelen hata"));
        Assert.Equal(305.50m, p.PaidTotal);
    }

    [Fact]
    public void Late_success_after_expiry_is_recorded_for_refund()
    {
        var p = Start(Draft());
        p.Expire();
        p.RecordLateSuccess(290, false);

        Assert.Equal(PaymentStatus.Succeeded, p.Status);
        Assert.Equal(290m, p.RefundableAmount);
        p.MarkRefunded(290);
        Assert.Equal(0m, p.RefundableAmount);
    }

    [Fact]
    public void Points_can_be_spent_again_after_failed_payment_refund()
    {
        var account = LoyaltyAccount.Open(Guid.NewGuid());
        account.Adjust(100, "test");
        var letterId = Guid.NewGuid();

        account.Spend(letterId, 40);
        Assert.Throws<DomainException>(() => account.Spend(letterId, 10)); // açık harcama varken
        account.RefundSpent(letterId, "Ödeme tamamlanmadı");
        Assert.Equal(100m, account.Balance);

        account.Spend(letterId, 60);               // yeni deneme
        Assert.Equal(40m, account.Balance);
        account.RefundSpent(letterId);              // iptal: son harcama iade
        account.RefundSpent(letterId);              // ikinci çağrı etkisiz
        Assert.Equal(100m, account.Balance);
    }
}
