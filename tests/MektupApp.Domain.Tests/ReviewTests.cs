using MektupApp.Domain.Common;
using MektupApp.Domain.Reviews;

namespace MektupApp.Domain.Tests;

public class ReviewTests
{
    [Theory]
    [InlineData("Ayşe Yılmaz", "Ayşe Y.")]
    [InlineData("Mehmet Ali İnce", "Mehmet Ali İ.")]
    [InlineData("Cem", "Cem")]
    [InlineData("  ", "Üye")]
    public void Public_name_hides_surname(string full, string expected) =>
        Assert.Equal(expected, PublicName.From(full));

    [Fact]
    public void Review_needs_delivered_letter_valid_rating_and_text()
    {
        Assert.Throws<DomainException>(() => CustomerReview.Write(Guid.NewGuid(), Guid.NewGuid(), false, "Ali Veli", "Ankara", 5, "Harika bir hizmet!"));
        Assert.Throws<DomainException>(() => CustomerReview.Write(Guid.NewGuid(), Guid.NewGuid(), true, "Ali Veli", "Ankara", 6, "Harika bir hizmet!"));
        Assert.Throws<DomainException>(() => CustomerReview.Write(Guid.NewGuid(), Guid.NewGuid(), true, "Ali Veli", "Ankara", 4, "kısa"));

        var r = CustomerReview.Write(Guid.NewGuid(), Guid.NewGuid(), true, "Ali Veli", "Ankara", 5, "  Harika bir hizmet!  ");
        Assert.Equal(ModerationStatus.Pending, r.Status);
        Assert.Equal("Ali V.", r.AuthorName);
        Assert.Equal("Harika bir hizmet!", r.Comment);
    }

    [Fact]
    public void Moderation_approve_and_reject()
    {
        var r = CustomerReview.Write(Guid.NewGuid(), Guid.NewGuid(), true, "Ali Veli", null, 4, "Çok memnun kaldım.");
        r.Reject("Uygunsuz ifade");
        Assert.Equal(ModerationStatus.Rejected, r.Status);
        Assert.Equal("Uygunsuz ifade", r.RejectionReason);
        r.Approve();
        Assert.True(r.IsApproved);
        Assert.Null(r.RejectionReason);
    }

    [Fact]
    public void Blog_comment_rules()
    {
        Assert.Throws<DomainException>(() => BlogComment.Write(Guid.NewGuid(), false, Guid.NewGuid(), "Ali Veli", "Güzel yazı", 0));
        Assert.Throws<DomainException>(() => BlogComment.Write(Guid.NewGuid(), true, Guid.NewGuid(), "Ali Veli", "Güzel yazı", BlogComment.MaxPendingPerPost));
        var c = BlogComment.Write(Guid.NewGuid(), true, Guid.NewGuid(), "Ali Veli", "Güzel yazı", 0);
        Assert.Equal(ModerationStatus.Pending, c.Status);
    }
}
