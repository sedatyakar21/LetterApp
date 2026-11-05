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
    public void Adjustment_cannot_make_balance_negative()
    {
        var acc = LoyaltyAccount.Open(Guid.NewGuid());
        acc.Adjust(20, "Hoş geldin hediyesi");
        Assert.Throws<DomainException>(() => acc.Adjust(-25, "Hatalı"));
        acc.Adjust(-20, "Geri alındı");
        Assert.Equal(0m, acc.Balance);
    }
}
