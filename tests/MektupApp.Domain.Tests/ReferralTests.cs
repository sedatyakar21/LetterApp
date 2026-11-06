using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Loyalty;

namespace MektupApp.Domain.Tests;

public class ReferralTests
{
    private static Customer NewCustomer(string email, AcquisitionChannel? channel = null, Customer? referrer = null) =>
        Customer.Register("Ali Veli", new Email(email), new PhoneNumber("05551234567"), "hash", false, channel, referrer);

    [Fact]
    public void Every_customer_gets_a_referral_code_of_initials_and_six_digits()
    {
        var c = NewCustomer("a@example.com"); // "Ali Veli"
        Assert.Matches("^AV[1-9][0-9]{5}$", c.ReferralCode);
    }

    [Theory]
    [InlineData("Ayşe Yılmaz", "AY")]
    [InlineData("Şükrü Öztürk", "SO")]
    [InlineData("İsmail Çelik", "IC")]
    [InlineData("ırmak ğüneş", "IG")]
    [InlineData("Ali Veli Can", "AC")]     // ilk ve son kelime
    [InlineData("  Cem  ", "CE")]           // tek kelime: ilk iki harf
    [InlineData("O", "OX")]
    public void Initials_are_ascii_and_uppercase(string fullName, string expected)
    {
        Assert.Equal(expected, ReferralCode.Initials(fullName));
    }

    [Fact]
    public void Typed_code_is_normalized()
    {
        Assert.Equal("AY482915", ReferralCode.Normalize(" ay 482-915 "));
        Assert.Equal("SI123456", ReferralCode.Normalize("şı123456"));
    }

    [Fact]
    public void Referrer_is_only_accepted_with_friend_channel()
    {
        var friend = NewCustomer("friend@example.com");
        Assert.Throws<DomainException>(() => NewCustomer("b@example.com", AcquisitionChannel.Radio, friend));

        var c = NewCustomer("c@example.com", AcquisitionChannel.Friend, friend);
        Assert.Equal(friend.Id, c.ReferredByCustomerId);
    }

    [Fact]
    public void Blocked_customer_code_cannot_be_used()
    {
        var friend = NewCustomer("friend@example.com");
        friend.Block();
        Assert.Throws<DomainException>(() => NewCustomer("d@example.com", AcquisitionChannel.Friend, friend));
    }

    [Fact]
    public void Referral_reward_uses_its_own_rule()
    {
        var s = LoyaltySettings.CreateDefault();
        s.Update(true, RewardType.Percentage, 10, "admin");
        s.UpdateReferral(true, RewardType.Fixed, 15, "admin");

        Assert.Equal(29m, s.CalculateReward(290m));
        Assert.Equal(15m, s.CalculateReferralReward(290m));

        s.UpdateReferral(false, RewardType.Fixed, 15, "admin");
        Assert.Equal(0m, s.CalculateReferralReward(290m));
    }

    [Fact]
    public void Shipped_letter_carries_referral_reward_and_earn_is_once_per_letter()
    {
        var address = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", address, new Recipient("Ayşe", RecipientType.Standard, null, address));
        letter.WriteContent("Merhaba");
        var referrerId = Guid.NewGuid();

        letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF", 0, 29, referrerId, 15);
        letter.StartProduction();
        letter.Ship("RR123456789TR");

        var shipped = Assert.Single(letter.DomainEvents.OfType<LetterShipped>());
        Assert.Equal(referrerId, shipped.ReferrerCustomerId);
        Assert.Equal(15m, shipped.ReferralPointsToEarn);

        var account = LoyaltyAccount.Open(referrerId);
        account.EarnReferral(letter.Id, 15, "Ali V.");
        account.EarnReferral(letter.Id, 15, "Ali V.");
        Assert.Equal(15m, account.Balance);
    }

    [Fact]
    public void Customer_cannot_be_own_referrer_on_letter()
    {
        var address = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var customerId = Guid.NewGuid();
        var letter = Letter.CreateDraft(customerId, "Ali", address, new Recipient("Ayşe", RecipientType.Standard, null, address));
        letter.WriteContent("Merhaba");
        Assert.Throws<DomainException>(() => letter.MarkPaid(new Money(290), PaymentMethod.Card, "REF", 0, 0, customerId, 10));
    }
}
