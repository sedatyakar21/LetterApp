using MektupApp.Domain.Campaigns;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Tests;

public class CampaignTests
{
    private static readonly DateOnly Nov10 = new(2026, 11, 10);

    private static Campaign Sample(DateOnly start, DateOnly? end, bool publish = true)
    {
        var c = Campaign.Create("Öğretmenler Günü'ne Özel", "Öğretmenlerinize mektup yazın", "Detay", start, end, "%15 İndirim", "ogretmen15");
        if (publish) c.Publish();
        return c;
    }

    [Fact]
    public void Phase_follows_dates_and_end_day_is_inclusive()
    {
        var c = Sample(Nov10, Nov10.AddDays(14));
        Assert.Equal(CampaignPhase.Upcoming, c.PhaseOn(Nov10.AddDays(-1)));
        Assert.Equal(CampaignPhase.Active, c.PhaseOn(Nov10));
        Assert.Equal(CampaignPhase.Active, c.PhaseOn(Nov10.AddDays(14)));   // son gün dahil
        Assert.Equal(CampaignPhase.Ended, c.PhaseOn(Nov10.AddDays(15)));
        Assert.Equal(0, c.DaysLeftOn(Nov10.AddDays(14)));
        Assert.Equal(14, c.DaysLeftOn(Nov10));
    }

    [Fact]
    public void Unpublished_is_draft_and_open_ended_never_ends()
    {
        Assert.Equal(CampaignPhase.Draft, Sample(Nov10, null, publish: false).PhaseOn(Nov10));
        var openEnded = Sample(Nov10, null);
        Assert.Equal(CampaignPhase.Active, openEnded.PhaseOn(Nov10.AddYears(3)));
        Assert.Null(openEnded.DaysLeftOn(Nov10));
    }

    [Fact]
    public void Slug_coupon_and_date_rules()
    {
        var c = Sample(Nov10, null);
        Assert.Equal("ogretmenler-gunu-ne-ozel", c.Slug);
        Assert.Equal("OGRETMEN15", c.CouponCode);
        Assert.Throws<DomainException>(() =>
            c.Edit("Başlık", "Özet", "", Nov10, Nov10.AddDays(-1), null, null));
    }

    private static Campaign Discounted(CampaignDiscountType type, decimal value, string? coupon = null,
        decimal? min = null, DateOnly? end = null, string title = "Kampanya")
    {
        var c = Campaign.Create(title, "Özet", "", Nov10, end ?? Nov10.AddDays(10), couponCode: coupon);
        c.SetDiscount(type, value, min);
        c.Publish();
        return c;
    }

    private static readonly CampaignCart Cart = new(Total: 400m, ScentAmount: 40m, PostcardAmount: 0m);

    [Fact]
    public void Discount_types_are_calculated_and_capped()
    {
        Assert.Equal(60m, Discounted(CampaignDiscountType.Percentage, 15).DiscountFor(Cart, Nov10));
        Assert.Equal(50m, Discounted(CampaignDiscountType.FixedAmount, 50).DiscountFor(Cart, Nov10));
        Assert.Equal(400m, Discounted(CampaignDiscountType.FixedAmount, 1000).DiscountFor(Cart, Nov10)); // tutarı aşmaz
        Assert.Equal(40m, Discounted(CampaignDiscountType.FreeScent, 0).DiscountFor(Cart, Nov10));
        Assert.Equal(0m, Discounted(CampaignDiscountType.FreePostcard, 0).DiscountFor(Cart, Nov10)); // sepette kartpostal yok
        Assert.Equal(0m, Discounted(CampaignDiscountType.None, 0).DiscountFor(Cart, Nov10));
    }

    [Fact]
    public void Discount_respects_dates_and_minimum_order()
    {
        var c = Discounted(CampaignDiscountType.FixedAmount, 50, min: 500);
        Assert.Equal(0m, c.DiscountFor(Cart, Nov10));
        Assert.Equal(50m, c.DiscountFor(Cart with { Total = 500 }, Nov10));
        Assert.Equal(0m, c.DiscountFor(Cart with { Total = 500 }, Nov10.AddDays(11))); // bitti
        Assert.Equal(0m, c.DiscountFor(Cart with { Total = 500 }, Nov10.AddDays(-1))); // başlamadı
    }

    [Fact]
    public void Invalid_discount_values_are_rejected()
    {
        var c = Sample(Nov10, null);
        Assert.Throws<DomainException>(() => c.SetDiscount(CampaignDiscountType.Percentage, 0, null));
        Assert.Throws<DomainException>(() => c.SetDiscount(CampaignDiscountType.Percentage, 101, null));
        Assert.Throws<DomainException>(() => c.SetDiscount(CampaignDiscountType.FixedAmount, -5, null));
        Assert.Throws<DomainException>(() => c.SetDiscount(CampaignDiscountType.FixedAmount, 10, -1));
    }

    [Fact]
    public void Policy_picks_best_single_campaign_and_coupon_ones_need_the_code()
    {
        var auto = Discounted(CampaignDiscountType.FixedAmount, 30, title: "Otomatik");
        var coupon = Discounted(CampaignDiscountType.Percentage, 20, coupon: "kis20", title: "Kuponlu");
        var all = new[] { auto, coupon };

        Assert.Equal("Otomatik", CampaignDiscountPolicy.Best(all, Cart, null, Nov10)!.Title);
        Assert.Equal("Otomatik", CampaignDiscountPolicy.Best(all, Cart, "YANLIS", Nov10)!.Title);

        var best = CampaignDiscountPolicy.Best(all, Cart, " kis20 ", Nov10)!;   // büyük/küçük harf ve boşluk önemsiz
        Assert.Equal("Kuponlu", best.Title);
        Assert.Equal(80m, best.Discount);

        Assert.Null(CampaignDiscountPolicy.Best(all, Cart, null, Nov10.AddDays(20)));
    }

    private static CampaignCustomer Member(DateOnly since, DateOnly? birth = null, params CampaignUse[] uses) =>
        new(since, birth, uses);

    [Fact]
    public void New_member_campaign_applies_within_days_of_registration_and_respects_usage_limit()
    {
        var c = Discounted(CampaignDiscountType.Percentage, 10);
        c.SetAudience(CampaignAudience.NewMembers, 30, usageLimitPerCustomer: 1);

        Assert.False(c.IsEligible(null, Nov10));                                   // giriş yapmamış / bilinmiyor
        Assert.True(c.IsEligible(Member(Nov10.AddDays(-30)), Nov10));               // 30. gün dahil
        Assert.False(c.IsEligible(Member(Nov10.AddDays(-31)), Nov10));
        Assert.False(c.IsEligible(Member(Nov10.AddDays(-3), null, new CampaignUse(c.Id, Nov10.AddDays(-1))), Nov10));
        // Başka kampanyanın kullanımı sayılmaz.
        Assert.True(c.IsEligible(Member(Nov10.AddDays(-3), null, new CampaignUse(Guid.NewGuid(), Nov10)), Nov10));
    }

    [Fact]
    public void Birthday_campaign_uses_window_and_renews_each_year()
    {
        var c = Discounted(CampaignDiscountType.FixedAmount, 50, end: Nov10.AddYears(3));
        c.SetAudience(CampaignAudience.Birthday, 7, usageLimitPerCustomer: 1);
        var birth = new DateOnly(1990, 11, 15);
        var member = Member(new DateOnly(2020, 1, 1), birth);

        Assert.True(c.IsEligible(member, Nov10));                 // doğum gününe 5 gün var
        Assert.True(c.IsEligible(member, new(2026, 11, 22)));     // 7 gün sonra, son gün
        Assert.False(c.IsEligible(member, new(2026, 11, 23)));
        Assert.False(c.IsEligible(Member(new DateOnly(2020, 1, 1)), Nov10)); // doğum tarihi yok

        var usedThisYear = Member(new DateOnly(2020, 1, 1), birth, new CampaignUse(c.Id, new(2026, 11, 12)));
        Assert.False(c.IsEligible(usedThisYear, new(2026, 11, 15)));
        Assert.True(c.IsEligible(usedThisYear, new(2027, 11, 15)));   // ertesi yıl hak yenilenir
    }

    [Fact]
    public void Birthday_window_handles_new_year_and_february_29()
    {
        Assert.Equal(new DateOnly(2026, 12, 28), Campaign.BirthdayWindowStart(new(1995, 1, 2), new(2027, 1, 1), 5));
        Assert.Equal(new DateOnly(2027, 2, 28), Campaign.BirthdayWindowStart(new(2000, 2, 29), new(2027, 2, 28), 0));
        Assert.Null(Campaign.BirthdayWindowStart(new(1995, 6, 1), Nov10, 7));
    }

    [Fact]
    public void Audience_rules_are_validated()
    {
        var c = Sample(Nov10, null);
        Assert.Throws<DomainException>(() => c.SetAudience(CampaignAudience.NewMembers, null, null));
        Assert.Throws<DomainException>(() => c.SetAudience(CampaignAudience.Birthday, 31, null));
        Assert.Throws<DomainException>(() => c.SetAudience(CampaignAudience.Everyone, null, 0));
        c.SetAudience(CampaignAudience.Everyone, 99, null);
        Assert.Null(c.AudienceDays);
    }

    [Fact]
    public void Policy_skips_campaigns_customer_is_not_eligible_for()
    {
        var everyone = Discounted(CampaignDiscountType.FixedAmount, 20, title: "Herkese");
        var birthday = Discounted(CampaignDiscountType.FixedAmount, 80, title: "Doğum günü");
        birthday.SetAudience(CampaignAudience.Birthday, 7, 1);
        var all = new[] { everyone, birthday };

        Assert.Equal("Herkese", CampaignDiscountPolicy.Best(all, Cart, null, Nov10, Member(Nov10.AddYears(-1)))!.Title);
        Assert.Equal("Doğum günü", CampaignDiscountPolicy.Best(all, Cart, null, Nov10,
            Member(Nov10.AddYears(-1), new DateOnly(1990, 11, 12)))!.Title);
    }
}
