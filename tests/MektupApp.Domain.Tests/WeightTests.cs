using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class WeightTests
{
    private static readonly PaperWeights W = new() { LetterSheet = 5, Photo = 12, Postcard = 4.5m, DocumentSheet = 5, Envelope = 6 };
    private static readonly PostalAddress Address = new("Atatürk Cad. No:1", "Nilüfer", "Bursa", null);

    [Fact]
    public void Envelope_only_for_empty_counts()
    {
        var e = LetterWeightPolicy.Estimate(0, 0, 0, false, W);
        Assert.Equal(6m, e.TotalGrams);
        Assert.Single(e.Lines);
    }

    [Fact]
    public void All_items_are_summed_and_rounded_up()
    {
        // zarf 6 + 3 yaprak × 5 + 2 fotoğraf × 12 + kartpostal 4,5 + 4 belge yaprağı × 5 = 69,5 → 70 g
        var e = LetterWeightPolicy.Estimate(3, 2, 4, true, W);
        Assert.Equal(69.5m, e.TotalGrams);
        Assert.Equal(70, e.RoundedGrams);
        Assert.Equal(["Zarf", "Mektup (A4)", "Fotoğraf", "Kartpostal", "Belge (yaprak)"], e.Lines.Select(l => l.Description));
    }

    [Fact]
    public void Letter_uses_pages_photos_postcard_and_document_sheets()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", Address, new Recipient("Veli", RecipientType.Standard, null, Address));
        letter.WriteContent(new string('a', LetterPages.CharsPerPage + 1)); // 2 sayfa
        letter.AddPhoto("p.jpg", "p.jpg");
        letter.AttachPostcard(new Postcard(PostcardCategory.Love, 1));
        letter.AddDocument("d1.pdf", "d1.pdf", pageCount: 3);
        letter.AddDocument("d2.png", "d2.png");

        // 6 + 2×5 + 12 + 4,5 + (3+1)×5 = 52,5
        Assert.Equal(52.5m, LetterWeightPolicy.Estimate(letter, W).TotalGrams);
    }

    [Fact]
    public void Document_page_count_is_at_least_one()
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", Address, new Recipient("Veli", RecipientType.Standard, null, Address));
        Assert.Equal(1, letter.AddDocument("x.pdf", "x.pdf", pageCount: 0).PageCount);
    }

    [Fact]
    public void Weight_settings_reject_out_of_range_values()
    {
        var s = WeightSettings.Create(new PaperWeights());
        Assert.Throws<DomainException>(() => s.Update(new PaperWeights { Envelope = -1 }, "admin"));
        Assert.Throws<DomainException>(() => s.Update(new PaperWeights { Photo = 1001 }, "admin"));

        s.Update(new PaperWeights { Envelope = 7.5m }, "admin");
        Assert.Equal(7.5m, s.Weights.Envelope);
        Assert.Equal("admin", s.UpdatedBy);
    }
}
