using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Postcards;

namespace MektupApp.Domain.Tests;

public class PostcardTests
{
    [Fact]
    public void Create_normalizes_category_and_is_active_by_default()
    {
        var p = PostcardDesign.Create("  Gül Bahçesi ", "  aşk   mektupları ", "img/1.jpg", 3);
        Assert.Equal("Gül Bahçesi", p.Title);
        Assert.Equal("Aşk mektupları", p.Category);
        Assert.True(p.IsActive);
        Assert.Equal(3, p.SortOrder);
    }

    [Fact]
    public void Turkish_lowercase_i_is_capitalized_with_dot()
    {
        Assert.Equal("İstanbul", PostcardDesign.NormalizeCategory("istanbul"));
    }

    [Theory]
    [InlineData("", "Aşk")]
    [InlineData("Ad", "")]
    public void Title_and_category_are_required(string title, string category) =>
        Assert.Throws<DomainException>(() => PostcardDesign.Create(title, category, "img/1.jpg"));

    [Fact]
    public void Sort_order_is_bounded() =>
        Assert.Throws<DomainException>(() => PostcardDesign.Create("Ad", "Aşk", "img/1.jpg", -1));

    [Fact]
    public void Replacing_image_returns_old_key()
    {
        var p = PostcardDesign.Create("Ad", "Aşk", "img/old.jpg");
        Assert.Equal("img/old.jpg", p.ReplaceImage("img/new.jpg"));
        Assert.Equal("img/new.jpg", p.ImageKey);
    }

    private static readonly PostalAddress Address = new("Atatürk Cad. 1", "Kadıköy", "İstanbul", null);

    private static Letter NewLetter() =>
        Letter.CreateDraft(Guid.NewGuid(), "Ali", Address, new Recipient("Veli", RecipientType.Standard, null, Address));

    private static Postcard Card(string title = "Kart") => new(Guid.NewGuid(), title, "Aşk");

    [Fact]
    public void Letter_can_hold_several_postcards_priced_and_weighed_per_card()
    {
        var letter = NewLetter();
        letter.SetPostcards([Card("A"), Card("B"), Card("C")]);

        Assert.Equal(["A", "B", "C"], letter.Postcards.Select(p => p.Title));
        var prices = new PriceList();
        var line = new LetterPricingPolicy().Calculate(letter, PaymentMethod.Card, prices).Lines.Single(l => l.Description.StartsWith("Kartpostal"));
        Assert.Equal("Kartpostal × 3", line.Description);
        Assert.Equal(prices.Postcard * 3, line.Amount.Amount);

        var weight = LetterWeightPolicy.Estimate(letter, new PaperWeights { Postcard = 4m }).Lines.Single(l => l.Description == "Kartpostal");
        Assert.Equal(3, weight.Count);
        Assert.Equal(12m, weight.Grams);
    }

    [Fact]
    public void Postcard_selection_can_be_cleared()
    {
        var letter = NewLetter();
        letter.SetPostcards([Card()]);
        letter.SetPostcards([]);
        Assert.Empty(letter.Postcards);
        Assert.DoesNotContain(new LetterPricingPolicy().Calculate(letter, PaymentMethod.Card, new PriceList()).Lines,
            l => l.Description.StartsWith("Kartpostal"));
    }

    [Fact]
    public void Too_many_or_duplicate_postcards_are_rejected()
    {
        var letter = NewLetter();
        Assert.Throws<DomainException>(() => letter.SetPostcards(Enumerable.Range(0, Letter.MaxPostcards + 1).Select(_ => Card())));
        var same = Card();
        Assert.Throws<DomainException>(() => letter.SetPostcards([same, same]));
    }

    [Fact]
    public void Letter_postcard_is_a_snapshot_of_catalog_entry()
    {
        var design = PostcardDesign.Create("Gül Bahçesi", "Aşk", "img/1.jpg");
        var snapshot = Postcard.From(design);
        design.Update("Yeni Ad", "Doğa", 0, isActive: false);

        Assert.Equal(design.Id, snapshot.DesignId);
        Assert.Equal("Gül Bahçesi", snapshot.Title);
        Assert.Equal("Gül Bahçesi (Aşk)", snapshot.DisplayText);
    }
}
