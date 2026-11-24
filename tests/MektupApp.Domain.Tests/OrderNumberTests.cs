using MektupApp.Domain.Letters;

namespace MektupApp.Domain.Tests;

public class OrderNumberTests
{
    [Fact]
    public void Format_is_first_8_characters_uppercase() =>
        Assert.Equal("04F731B2", OrderNumber.Format(Guid.Parse("04f731b2-9cfe-40f7-9249-db6b5e3f819a")));

    [Theory]
    [InlineData("#04f731b2", "04F731B2")]
    [InlineData(" #04F7 ", "04F7")]       // "#" ile kısa önek
    [InlineData("04f731", "04F731")]      // "#" olmadan en az 6 karakter
    [InlineData("04F731B2", "04F731B2")]
    public void Order_number_queries_become_prefixes(string query, string expected) =>
        Assert.Equal(expected, OrderNumber.AsSearchPrefix(query));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bade")]        // onaltılık ama isim olabilir: "#" olmadan kısa
    [InlineData("Ankara")]
    [InlineData("#04F")]        // çok kısa
    [InlineData("04F731B2C")]   // çok uzun
    [InlineData("RR123456789TR")]
    public void Other_queries_are_not_order_numbers(string? query) =>
        Assert.Null(OrderNumber.AsSearchPrefix(query));
}
