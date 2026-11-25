using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

/// <summary>Taslak, alıcı / gönderen bilgileri eksikken kaydedilebilir; ödemeye geçerken hepsi aranır.</summary>
public class DraftTests
{
    private static readonly PostalAddress Full = new("Atatürk Cad. 1", "Kadıköy", "İstanbul", null);

    private static Letter EmptyContactsDraft(string content = "Merhaba")
    {
        var letter = Letter.CreateDraft(Guid.NewGuid(), "", PostalAddress.Draft(null, null, null, null),
            Recipient.Draft(null, RecipientType.Standard, null, PostalAddress.Draft(null, null, null, null)));
        letter.WriteContent(content);
        return letter;
    }

    [Fact]
    public void Draft_can_be_saved_without_contacts_but_cannot_be_paid()
    {
        var letter = EmptyContactsDraft();
        Assert.False(letter.HasCompleteContacts);
        var ex = Assert.Throws<DomainException>(letter.EnsureReadyForCheckout);
        Assert.Contains("alıcı", ex.Message);
    }

    [Fact]
    public void Missing_sender_blocks_checkout()
    {
        var letter = EmptyContactsDraft();
        letter.UpdateEnvelope("", PostalAddress.Draft(null, null, null, null), new Recipient("Veli", RecipientType.Standard, null, Full));
        var ex = Assert.Throws<DomainException>(letter.EnsureReadyForCheckout);
        Assert.Contains("gönderen", ex.Message);
    }

    [Fact]
    public void Completed_contacts_allow_checkout()
    {
        var letter = EmptyContactsDraft();
        letter.UpdateEnvelope("Ali", Full, Recipient.Draft("Veli", RecipientType.Standard, null, Full));
        Assert.True(letter.HasCompleteContacts);
        letter.EnsureReadyForCheckout();
    }

    [Fact]
    public void Soldier_draft_without_unit_is_incomplete()
    {
        var r = Recipient.Draft("Er Ahmet", RecipientType.Soldier, null, Full);
        Assert.False(r.IsComplete);
        var letter = EmptyContactsDraft();
        letter.UpdateEnvelope("Ali", Full, r);
        Assert.Contains("birlik", Assert.Throws<DomainException>(letter.EnsureReadyForCheckout).Message);
    }

    [Fact]
    public void Draft_address_still_checks_lengths_and_postal_code()
    {
        Assert.Throws<DomainException>(() => PostalAddress.Draft(new string('a', 251), null, null, null));
        Assert.Throws<DomainException>(() => PostalAddress.Draft(null, null, null, "12"));
        Assert.False(PostalAddress.Draft("Cad.", null, "İstanbul", null).IsComplete);
        Assert.True(Full.IsComplete);
    }

    [Fact]
    public void Strict_address_constructor_still_rejects_blanks() =>
        Assert.Throws<DomainException>(() => new PostalAddress("", "Kadıköy", "İstanbul", null));
}
