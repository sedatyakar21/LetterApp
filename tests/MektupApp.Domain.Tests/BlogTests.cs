using MektupApp.Domain.Blog;
using MektupApp.Domain.Common;

namespace MektupApp.Domain.Tests;

public class BlogTests
{
    [Theory]
    [InlineData("Askere Mektup Nasıl Gönderilir?", "askere-mektup-nasil-gonderilir")]
    [InlineData("  Çiçek, Şükran & Güzel Söz!  ", "cicek-sukran-guzel-soz")]
    [InlineData("İlk Mektubum 2026", "ilk-mektubum-2026")]
    [InlineData("---", "")]
    public void Slug_is_url_friendly(string input, string expected) =>
        Assert.Equal(expected, BlogSlug.From(input));

    [Fact]
    public void Draft_gets_slug_from_title_and_publish_keeps_first_date()
    {
        var post = BlogPost.CreateDraft("Cezaevine Mektup Yazarken", "Kısa özet", "İçerik metni", "BiMektup");
        Assert.Equal("cezaevine-mektup-yazarken", post.Slug);
        Assert.False(post.IsPublished);

        post.Publish();
        var first = post.PublishedAt;
        post.Unpublish();
        post.Publish();
        Assert.Equal(first, post.PublishedAt);
    }

    [Fact]
    public void Required_fields_and_invalid_slug_are_rejected()
    {
        Assert.Throws<DomainException>(() => BlogPost.CreateDraft("", "özet", "içerik", "yazar"));
        Assert.Throws<DomainException>(() => BlogPost.CreateDraft("Başlık", "özet", "içerik", "yazar", slug: "!!!"));
    }

    [Fact]
    public void Reading_time_is_at_least_one_minute()
    {
        var shortPost = BlogPost.CreateDraft("Kısa", "özet", "birkaç kelime", "yazar");
        var longPost = BlogPost.CreateDraft("Uzun", "özet", string.Join(' ', Enumerable.Repeat("kelime", 450)), "yazar");
        Assert.Equal(1, shortPost.ReadingMinutes);
        Assert.Equal(3, longPost.ReadingMinutes);
    }

    [Fact]
    public void Replacing_cover_returns_old_key_for_cleanup()
    {
        var post = BlogPost.CreateDraft("Başlık", "özet", "içerik", "yazar");
        Assert.Null(post.SetCover("a.jpg"));
        Assert.Equal("a.jpg", post.SetCover("b.jpg"));
        Assert.Equal("b.jpg", post.SetCover(null));
    }
}
