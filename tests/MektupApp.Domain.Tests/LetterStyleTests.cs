using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class LetterStyleTests
{
    [Fact]
    public void Text_size_defaults_to_medium()
    {
        Assert.Equal(TextSize.Medium, LetterStyle.Default.Size);
        Assert.Equal(TextSize.Medium, new LetterStyle(PaperColor.Blue, EnvelopeColor.Red, LetterFont.Sans, InkColor.Navy).Size);
    }

    [Fact]
    public void Letter_keeps_chosen_text_size()
    {
        var address = new PostalAddress("Atatürk Cad. 1", "Nilüfer", "Bursa", "16110");
        var letter = Letter.CreateDraft(Guid.NewGuid(), "Ali", address, new Recipient("Ayşe", RecipientType.Standard, null, address));
        letter.ChangeStyle(new LetterStyle(PaperColor.White, EnvelopeColor.White, LetterFont.Serif, InkColor.Black, TextSize.Large));
        Assert.Equal(TextSize.Large, letter.Style.Size);
    }
}
