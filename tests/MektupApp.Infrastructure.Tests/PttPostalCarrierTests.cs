using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using MektupApp.Application.Abstractions;
using MektupApp.Infrastructure.Carriers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Tests;

/// <summary>
/// PTT adapter'ı gerçek servise gitmeden test edilir: istekler sahte bir HTTP handler'da yakalanır
/// ve PTT'nin yayınladığı WSDL içindeki XML şemasına karşı doğrulanır.
/// </summary>
public class PttPostalCarrierTests
{
    private const string Kabul = "http://kabul.ptt.gov.tr";
    private const string Takip = "http://takip.ptt.gov.tr";

    private static readonly PttOptions Options = new()
    {
        Enabled = true,
        UseTestEnvironment = true,
        CustomerId = 123456,
        Username = "PttWs",
        Password = "test-sifre"
    };

    private static CarrierShipmentRequest SampleRequest() => new(
        Guid.NewGuid(),
        "RR100000003TR",
        "ABC123DEF456",
        new CarrierParty("Ayşe Yılmaz", "3. Piyade Tugayı - Askeri Posta 1", "Merkez", "Erzurum", "25000"),
        new CarrierParty("Ali Veli", "Atatürk Cad. 1", "Nilüfer", "Bursa", "16110", "5551234567", "ali@example.com"),
        WeightGrams: 25);

    [Fact]
    public async Task Create_shipment_request_is_valid_against_ptt_wsdl_schema()
    {
        var handler = new CapturingHandler(SoapResponse(Kabul, "kabulEkle2Response",
            "<ns:return><ax:aciklama>BASARILI</ax:aciklama><ax:dongu><ax:barkod>RR100000003TR</ax:barkod>" +
            "<ax:donguAciklama>OK</ax:donguAciklama><ax:donguHataKodu>1</ax:donguHataKodu><ax:donguSonuc>true</ax:donguSonuc>" +
            "</ax:dongu><ax:hataKodu>1</ax:hataKodu></ns:return>", Kabul + "/xsd"));
        var carrier = Create(handler);

        var result = await carrier.CreateShipmentAsync(SampleRequest());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("RR100000003TR", result.Barcode);
        Assert.StartsWith("MEKTUP-", result.CarrierReference);

        Assert.Equal("https://pttws.ptt.gov.tr/PttVeriYuklemeTest/services/Sorgu", handler.Url);
        Assert.Equal("urn:kabulEkle2", handler.SoapAction);

        var operation = ExtractOperation(handler.Body!, "kabulEkle2");
        ValidateAgainstWsdl("PttVeriYuklemeTest.wsdl", operation);

        var input = operation.Elements().Single();
        Assert.Equal(["dongu", "dosyaAdi", "gonderiTip", "gonderiTur", "kullanici", "musteriId", "sifre"],
            input.Elements().Select(e => e.Name.LocalName));
        var item = input.Elements().First();
        Assert.Equal("Ayşe Yılmaz", item.Elements().Single(e => e.Name.LocalName == "aliciAdi").Value);
        Assert.Equal("25", item.Elements().Single(e => e.Name.LocalName == "agirlik").Value);
    }

    [Fact]
    public async Task Create_shipment_reports_ptt_rejection()
    {
        var handler = new CapturingHandler(SoapResponse(Kabul, "kabulEkle2Response",
            "<ns:return><ax:aciklama>HATALI</ax:aciklama><ax:dongu><ax:barkod>RR100000003TR</ax:barkod>" +
            "<ax:donguAciklama>Barkod numarası size tahsis edilmemiş</ax:donguAciklama><ax:donguHataKodu>-2</ax:donguHataKodu>" +
            "<ax:donguSonuc>false</ax:donguSonuc></ax:dongu><ax:hataKodu>-1</ax:hataKodu></ns:return>", Kabul + "/xsd"));

        var result = await Create(handler).CreateShipmentAsync(SampleRequest());

        Assert.False(result.Succeeded);
        Assert.Equal("Barkod numarası size tahsis edilmemiş", result.Error);
    }

    [Fact]
    public async Task Tracking_request_is_valid_and_delivery_is_detected()
    {
        var handler = new CapturingHandler(SoapResponse(Takip, "gonderiSorgu2Response",
            "<ns:return><ax:BARNO>RR100000003TR</ax:BARNO><ax:TESALAN>AYŞE YILMAZ</ax:TESALAN>" +
            "<ax:dongu><ax:IMERK>ERZURUM DAĞITIM</ax:IMERK><ax:ISLEM>TESLİM EDİLDİ</ax:ISLEM><ax:ITARIH>07/11/2026 14:30:00</ax:ITARIH><ax:siraNo>3</ax:siraNo></ax:dongu>" +
            "<ax:dongu><ax:IMERK>BURSA PİM</ax:IMERK><ax:ISLEM>KABUL EDİLDİ</ax:ISLEM><ax:ITARIH>06/11/2026 09:00:00</ax:ITARIH><ax:siraNo>1</ax:siraNo></ax:dongu>" +
            "<ax:sonucAciklama>BASARILI</ax:sonucAciklama><ax:sonucKodu>1</ax:sonucKodu></ns:return>", Takip + "/xsd"));
        var carrier = Create(handler);

        var result = await carrier.TrackAsync("RR100000003TR");

        Assert.Equal("https://pttws.ptt.gov.tr/GonderiTakipV2Test/services/Sorgu", handler.Url);
        Assert.Equal("urn:gonderiSorgu2", handler.SoapAction);
        ValidateAgainstWsdl("GonderiTakipV2Test.wsdl", ExtractOperation(handler.Body!, "gonderiSorgu2"));

        Assert.True(result.Found);
        Assert.True(result.Delivered);
        Assert.Equal("AYŞE YILMAZ", result.DeliveredTo);
        Assert.Equal(["KABUL EDİLDİ", "TESLİM EDİLDİ"], result.Movements.Select(m => m.Description)); // siraNo'ya göre sıralı
        Assert.Equal(new DateTime(2026, 11, 7, 11, 30, 0, DateTimeKind.Utc), result.Latest!.At);      // TR saati → UTC
    }

    [Fact]
    public async Task Tracking_in_transit_is_not_delivered()
    {
        var handler = new CapturingHandler(SoapResponse(Takip, "gonderiSorgu2Response",
            "<ns:return><ax:dongu><ax:IMERK>BURSA PİM</ax:IMERK><ax:ISLEM>KABUL EDİLDİ</ax:ISLEM><ax:ITARIH>06.11.2026 09:00</ax:ITARIH><ax:siraNo>1</ax:siraNo></ax:dongu>" +
            "<ax:sonucKodu>1</ax:sonucKodu></ns:return>", Takip + "/xsd"));

        var result = await Create(handler).TrackAsync("RR100000003TR");

        Assert.True(result.Found);
        Assert.False(result.Delivered);
        Assert.Equal("BURSA PİM", result.Latest!.Location);
    }

    [Fact]
    public async Task Soap_fault_is_returned_as_error_without_throwing()
    {
        var fault = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault>" +
                    "<faultcode>soapenv:Server</faultcode><faultstring>Kullanici adi veya sifre hatali</faultstring>" +
                    "</soapenv:Fault></soapenv:Body></soapenv:Envelope>";
        var result = await Create(new CapturingHandler(fault, HttpStatusCode.InternalServerError)).TrackAsync("RR100000003TR");

        Assert.False(result.Found);
        Assert.Equal("Kullanici adi veya sifre hatali", result.Error);
    }

    [Theory]
    [InlineData("TESLİM EDİLDİ", true)]
    [InlineData("Teslim edildi", true)]
    [InlineData("TESLİM EDİLEMEDİ - ADRESTE BULUNAMADI", false)]
    [InlineData("DAĞITIMA ÇIKARILDI", false)]
    public void Delivery_text_detection(string text, bool expected) =>
        Assert.Equal(expected, PttPostalCarrier.IsDeliveredText(text));

    [Fact]
    public void Barcode_formats()
    {
        Assert.Equal("RR100000003TR", BarcodeAllocator.Format(new BarcodeOptions(), 10_000_000));
        Assert.Equal("KP00000001234", BarcodeAllocator.Format(new BarcodeOptions { Format = "Cargo", Prefix = "KP" }, 1234));
    }

    // ---------------- yardımcılar ----------------

    private static PttPostalCarrier Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(Options), NullLogger<PttPostalCarrier>.Instance);

    private static string SoapResponse(string ns, string element, string inner, string axNs) =>
        $"<?xml version='1.0' encoding='UTF-8'?><soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
        $"<soapenv:Body><ns:{element} xmlns:ns=\"{ns}\" xmlns:ax=\"{axNs}\">{inner}</ns:{element}></soapenv:Body></soapenv:Envelope>";

    private static XElement ExtractOperation(string soapXml, string operation)
    {
        var doc = XDocument.Parse(soapXml);
        Assert.Equal("Envelope", doc.Root!.Name.LocalName);
        return doc.Descendants().Single(e => e.Name.LocalName == operation);
    }

    /// <summary>WSDL içindeki xs:schema bölümlerini yükleyip SOAP gövdesindeki işlemi doğrular.</summary>
    private static void ValidateAgainstWsdl(string wsdlFile, XElement operation)
    {
        var wsdl = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", wsdlFile));
        XNamespace xs = "http://www.w3.org/2001/XMLSchema";
        var schemas = new XmlSchemaSet();
        foreach (var schema in wsdl.Descendants(xs + "schema"))
        {
            // WSDL kök elemanındaki namespace tanımlarını şemaya taşı (ör. ax21:, ax22: önekleri).
            foreach (var attr in wsdl.Root!.Attributes().Where(a => a.IsNamespaceDeclaration))
                if (schema.Attribute(attr.Name) is null) schema.SetAttributeValue(attr.Name, attr.Value);
            using var reader = schema.CreateReader();
            schemas.Add(XmlSchema.Read(reader, null)!);
        }
        schemas.Compile();

        var errors = new List<string>();
        new XDocument(new XElement(operation)).Validate(schemas, (_, e) => errors.Add(e.Message));
        Assert.True(errors.Count == 0, "WSDL şema hataları:\n" + string.Join("\n", errors));
    }

    private sealed class CapturingHandler(string responseXml, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? SoapAction { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.ToString();
            SoapAction = request.Headers.TryGetValues("SOAPAction", out var v) ? v.Single() : null;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(responseXml, Encoding.UTF8, "text/xml") };
        }
    }
}
