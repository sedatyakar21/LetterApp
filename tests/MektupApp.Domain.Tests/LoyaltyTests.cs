using MektupApp.Domain.Common;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Domain.Tests;

public class LoyaltyTests
{
    [Fact]
    public void Percentage_reward_is_based_on_charged_amount()
    {
        var s = LoyaltySettings.CreateDefault();
        s.Update(true, RewardType.Percentage, 10, "admin");
        Assert.Equal(30.50m, s.CalculateReward(305m));
    }

    [Fact]
    public void Fixed_reward_is_same_for_every_letter()
    {
        var s = LoyaltySettings.CreateDefault();
        s.Update(true, RewardType.Fixed, 25, "admin");
        Assert.Equal(25m, s.CalculateReward(0m));
        Assert.Equal(25m, s.CalculateReward(1000m));
    }

    [Fact]
    public void Disabled_program_gives_no_points_and_invalid_values_are_rejected()
    {
        var s = LoyaltySettings.CreateDefault();
        Assert.Equal(0m, s.CalculateReward(500m));
        Assert.Throws<DomainException>(() => s.Update(true, RewardType.Percentage, 150, "admin"));
        Assert.Throws<DomainException>(() => s.Update(true, RewardType.Fixed, -1, "admin"));
    }

    [Fact]
    public void Earn_is_idempotent_per_letter()
    {
        var acc = LoyaltyAccount.Open(Guid.NewGuid());
        var letter = Guid.NewGuid();
        acc.Earn(letter, 15);
        acc.Earn(letter, 15);
        Assert.Equal(15m, acc.Balance);
        Assert.Single(acc.Transactions);
    }

    [Fact]
    public void Cannot_spend_more_than_balance_and_refund_restores_points()
    {
        var acc = LoyaltyAccount.Open(Guid.NewGuid());
        acc.Earn(Guid.NewGuid(), 50);
        var letter = Guid.NewGuid();

        Assert.Throws<DomainException>(() => acc.Spend(letter, 60));
        acc.Spend(letter, 40);
        Assert.Equal(10m, acc.Balance);

        acc.RefundSpent(letter);
        acc.RefundSpent(letter); // ikinci iade etkisiz
        Assert.Equal(50m, acc.Balance);
    }

    [Fact]
    public void Point_value_converts_points_to_money()
    {
        var s = LoyaltySettings.CreateDefault();
        Assert.Equal(1m, s.PointValue);
        Assert.Equal(29m, s.ToMoney(29));

        s.UpdatePointValue(0.5m, "admin");
        Assert.Equal(14.50m, s.ToMoney(29));
    }

    [Fact]
    public void Paid_letter_records_points_and_their_money_value()
    {
        var address = new MektupApp.Domain.Letters.PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var letter = MektupApp.Domain.Letters.Letter.CreateDraft(Guid.NewGuid(), "Ali", address,
            new MektupApp.Domain.Letters.Recipient("Ayşe", MektupApp.Domain.Letters.RecipientType.Standard, null, address));
        letter.WriteContent("Merhaba");

        letter.MarkPaid(new Money(264.25m), MektupApp.Domain.Letters.PaymentMethod.Card, "REF",
            pointsUsed: 51.5m, pointsDiscount: 25.75m);

        Assert.Equal(51.5m, letter.PointsUsed);
        Assert.Equal(25.75m, letter.PointsDiscount);
    }

    [Fact]
    public void Point_value_must_be_in_range()
    {
        var s = LoyaltySettings.CreateDefault();
        Assert.Throws<DomainException>(() => s.UpdatePointValue(0, "admin"));
        Assert.Throws<DomainException>(() => s.UpdatePointValue(-1, "admin"));
        Assert.Throws<DomainException>(() => s.UpdatePointValue(5000, "admin"));
    }

    [Theory]
    // değer, toplam, bakiye → kullanılan puan, indirim
    [InlineData(1.0, 290, 51.5, 51.5, 51.5)]      // bakiye yetmiyor: hepsi kullanılır
    [InlineData(0.5, 290, 51.5, 51.5, 25.75)]     // 1 puan = 0,50 ₺
    [InlineData(0.5, 290, 1000, 580, 290)]        // tutarın tamamı: 290 / 0,5 = 580 puan
    [InlineData(2.0, 290, 1000, 145, 290)]        // 1 puan = 2 ₺
    [InlineData(3.0, 100, 1000, 33.34, 100)]      // yukarı yuvarlanır, indirim tutarı aşmaz
    [InlineData(1.0, 290, 0, 0, 0)]
    public void Redeem_uses_point_value(double value, double total, double balance, double expectedPoints, double expectedDiscount)
    {
        var s = LoyaltySettings.CreateDefault();
        s.UpdatePointValue((decimal)value, "admin");

        var r = s.Redeem((decimal)total, (decimal)balance);

        Assert.Equal((decimal)expectedPoints, r.Points);
        Assert.Equal((decimal)expectedDiscount, r.Discount);
    }

    [Fact]
    public void Adjustment_cannot_make_balance_negative()
    {
        var acc = LoyaltyAccount.Open(Guid.NewGuid());
        acc.Adjust(20, "Hoş geldin hediyesi");
        Assert.Throws<DomainException>(() => acc.Adjust(-25, "Hatalı"));
        acc.Adjust(-20, "Geri alındı");
        Assert.Equal(0m, acc.Balance);
    }
}
