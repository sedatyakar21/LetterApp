using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Scents;

namespace MektupApp.Domain.Tests;

public class ScentTests
{
    private static readonly PostalAddress Address = new("Atatürk Cad. 1", "Kadıköy", "İstanbul", null);

    private static Letter NewLetter() =>
        Letter.CreateDraft(Guid.NewGuid(), "Ali", Address, new Recipient("Veli", RecipientType.Standard, null, Address));

    [Fact]
    public void Create_trims_name_and_is_active_by_default()
    {
        var s = ScentOption.Create("  Vanilya ", 4);
        Assert.Equal("Vanilya", s.Name);
        Assert.True(s.IsActive);
        Assert.Equal(4, s.SortOrder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name) => Assert.Throws<DomainException>(() => ScentOption.Create(name));

    [Fact]
    public void Sort_order_is_bounded() => Assert.Throws<DomainException>(() => ScentOption.Create("Gül", 10_000));

    [Fact]
    public void Letter_scent_is_a_snapshot_and_priced_once()
    {
        var option = ScentOption.Create("Gül");
        var letter = NewLetter();
        letter.ChooseScent(LetterScent.From(option));
        option.Update("Kırmızı Gül", 0, isActive: false);

        Assert.Equal("Gül", letter.Scent!.Name);
        Assert.Equal(option.Id, letter.Scent.ScentId);
        var prices = new PriceList();
        var line = new LetterPricingPolicy().Calculate(letter, PaymentMethod.Card, prices).Lines.Single(l => l.Description == "Koku");
        Assert.Equal(prices.Scent, line.Amount.Amount);
    }

    [Fact]
    public void No_scent_means_no_price_line()
    {
        var letter = NewLetter();
        letter.ChooseScent(new LetterScent(Guid.NewGuid(), "Gül"));
        letter.ChooseScent(null);
        Assert.Null(letter.Scent);
        Assert.DoesNotContain(new LetterPricingPolicy().Calculate(letter, PaymentMethod.Card, new PriceList()).Lines, l => l.Description == "Koku");
    }
}
