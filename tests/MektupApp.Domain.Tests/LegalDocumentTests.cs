using MektupApp.Domain.Common;
using MektupApp.Domain.Legal;

namespace MektupApp.Domain.Tests;

public class LegalDocumentTests
{
    [Fact]
    public void Slug_is_generated_from_title()
    {
        var d = LegalDocument.Create("Mesafeli Satış Sözleşmesi", null, "metin", true, 1, "admin");
        Assert.Equal("mesafeli-satis-sozlesmesi", d.Slug);
        Assert.Equal(1, d.Version);
        Assert.False(d.IsPublished);
    }

    [Fact]
    public void Version_increases_only_when_content_changes()
    {
        var d = LegalDocument.Create("Belge", "belge", "ilk metin", true, 1, "admin");
        d.Update("Yeni Başlık", null, "ilk metin", false, 2, "admin");
        Assert.Equal(1, d.Version);
        d.Update("Yeni Başlık", null, "değişen metin", false, 2, "admin");
        Assert.Equal(2, d.Version);
    }

    [Fact]
    public void System_document_slug_is_locked_and_not_deletable()
    {
        var d = LegalDocument.Create("Üyelik Sözleşmesi", LegalDocument.MembershipSlug, "metin", true, 1, "sistem", isSystem: true);
        Assert.Throws<DomainException>(() => d.Update("Üyelik", "baska-adres", "metin", true, 1, "admin"));
        Assert.Throws<DomainException>(d.EnsureDeletable);
        d.Update("Üyelik Sözleşmesi (güncel)", null, "yeni metin", true, 1, "admin");
        Assert.Equal(LegalDocument.MembershipSlug, d.Slug);
    }

    [Fact]
    public void Empty_document_cannot_be_published_or_emptied_while_published()
    {
        var d = LegalDocument.Create("Boş", "bos", "", false, 1, "admin");
        Assert.Throws<DomainException>(d.Publish);
        d.Update("Boş", null, "artık dolu", false, 1, "admin");
        d.Publish();
        Assert.Throws<DomainException>(() => d.Update("Boş", null, "  ", false, 1, "admin"));
    }
}
