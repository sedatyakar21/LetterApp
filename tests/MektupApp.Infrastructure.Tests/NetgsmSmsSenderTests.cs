using System.Xml.Linq;
using MektupApp.Infrastructure.Notifications;

namespace MektupApp.Infrastructure.Tests;

public class NetgsmSmsSenderTests
{
    private static readonly NetgsmOptions Options = new() { UserCode = "8501234567", Password = "p&ss<x>", Header = "BIMEKTUP" };

    [Fact]
    public void Request_is_valid_xml_with_turkish_encoding_and_informational_filter()
    {
        var xml = NetgsmSmsSender.BuildRequest(Options, "5551234567", "Siparişiniz alındı & yola çıktı <3");
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);

        var doc = XDocument.Parse(xml);
        var header = doc.Root!.Element("header")!;
        Assert.Equal("TR", header.Element("company")!.Attribute("dil")!.Value);
        Assert.Equal("p&ss<x>", header.Element("password")!.Value);       // özel karakterler kaçışlanır
        Assert.Equal("BIMEKTUP", header.Element("msgheader")!.Value);
        Assert.Equal("0", header.Element("filter")!.Value);                // ticari değil (İYS dışı)
        Assert.Equal("1:n", header.Element("type")!.Value);

        var body = doc.Root.Element("body")!;
        Assert.Equal("Siparişiniz alındı & yola çıktı <3", body.Element("msg")!.Value);
        Assert.Equal("5551234567", body.Element("no")!.Value);
    }

    [Theory]
    [InlineData("00 1234567890", true, false, "1234567890")]
    [InlineData("00 1234567890\n", true, false, "1234567890")]
    [InlineData("30", false, true, null)]
    [InlineData("40", false, true, null)]
    [InlineData("80", false, false, null)]
    [InlineData("<html>bakım</html>", false, false, null)]
    public void Response_codes_are_interpreted(string body, bool success, bool permanent, string? messageId)
    {
        var r = NetgsmSmsSender.ParseResponse(body);
        Assert.Equal(success, r.Success);
        Assert.Equal(permanent, r.Permanent);
        Assert.Equal(messageId, r.MessageId);
    }
}
