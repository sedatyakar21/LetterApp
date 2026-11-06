# MektupApp

Online mektup gönderme uygulaması: kullanıcı mektubunu yazar, kağıt/zarf rengi, yazı stili, koku, kartpostal ve fotoğraf seçer, öder. Mektup basılıp postaya verilir, takip kodu SMS ile gelir.

ASP.NET Core MVC (.NET 10) + EF Core + Microsoft SQL Server, Domain-Driven Design katmanlarıyla.

## Çalıştırma

```bash
dotnet run --project src/MektupApp.Web
```

- Uygulama: http://localhost:5212
- Veritabanı: `appsettings.json` → `ConnectionStrings:Default` (varsayılan: yerel SQL Server, Windows kimlik doğrulaması, `MektupApp` veritabanı). Farklı bir sunucu için bu değeri veya `ConnectionStrings__Default` ortam değişkenini değiştirin.
- Uygulama açılışta bekleyen EF Core migration'larını otomatik uygular; veritabanı yoksa oluşturur.
- Geliştirme ortamında yönetici hesabı `appsettings.Development.json` içindeki `Seed` bölümünden oluşturulur (yönetim paneli: `/yonetim`).

### Yönetim paneli (`/yonetim`)

Sadece `Admin` rolündeki kullanıcılar erişebilir (MVC Area: `src/MektupApp.Web/Areas/Admin`).

- **Genel Bakış:** bekleyen işler (basım / postalama / teslimat), bugün / bu ay / toplam ciro, son 14 günün günlük ciro grafiği, son siparişler.
- **Siparişler:** duruma göre sekmeler, alıcı / gönderen / il / takip kodu ile arama, sayfalama, toplu "Basıma Al", sipariş detayı (mektup önizleme, fotoğraflar, durum geçmişi), yazdırma görünümü, postaya verme, teslim ve gerekçeli iptal.
- **Müşteriler:** ad / e-posta / telefon ile arama, sipariş sayısı ve harcama, hesabı engelleme / açma, yönetici yetkisi verme / kaldırma. Engellenen kullanıcının açık oturumu bir sonraki istekte kapanır; yönetici kendi hesabını kilitleyemez.
- **Fiyatlar:** fiyat listesi veritabanında tutulur ve panelden değiştirilir; değişiklik yalnızca sonraki ödemeleri etkiler. `appsettings.json` → `Pricing` sadece ilk kurulumda kullanılan varsayılandır.

- **MektupPuan:** puan programını açma / kapama ve kazanım kuralı: ödenen tutarın yüzdesi **veya** mektup başına sabit puan. Müşteri detayında bakiye, puan hareketleri ve elle düzeltme (+/−, açıklamalı).

### MektupPuan (sadakat puanı)

- Kazanılacak puan **ödeme anında** o anki ayara göre hesaplanıp mektuba yazılır (ödeme ekranında gösterilir); puan, mektup **postaya verildiğinde** (`LetterShipped` olayı) hesaba geçer. Ödeme sonrası iptal edilen mektup puan kazandırmaz.
- **Puan değeri:** 1 puanın TL karşılığı admin → MektupPuan → "Puan değeri" bölümünden ayarlanır (varsayılan 1 puan = 1 ₺, 0,01–1.000 ₺ arası). Kazanım kuralları puan cinsindendir; değer sadece puan harcanırken TL'ye çevirmede kullanılır. Ödemede kullanılan puan ve TL indirim ayrı ayrı kaydedilir (`PointsUsed`, `PointsDiscount`), iptalde puan olarak iade edilir. Değeri değiştirmek mevcut bakiyelerin TL karşılığını da değiştirir.
- Müşteri ödeme adımında bakiyesini kullanabilir; tutarın tamamı puanla karşılanırsa ödeme sağlayıcısına gidilmez. Yüzdesel kazanım sadece kart / mobil ile ödenen kısım üzerinden hesaplanır.
- İptal edilen siparişte kullanılan puan otomatik iade edilir (`LetterCancelledAfterPayment` olayı).
- **Referans (arkadaş daveti):** her üyenin benzersiz ve akılda kalıcı bir referans no'su vardır: ad-soyad baş harfleri + 6 haneli sayı (ör. "Ayşe Yılmaz" → `AY482915`; Türkçe harfler sadeleştirilir, yazarken boşluk / küçük harf / Türkçe harf fark etmez) ve "Puanlarım" sayfasında davet linkiyle (`/kayit?ref=MK...`) paylaşılır. Kayıtta "Bizi nereden duydunuz?" (Sosyal medya / Radyo / Arkadaş / Diğer) zorunludur; "Arkadaş" seçilince isteğe bağlı referans no alanı açılır, girilirse geçerli olmak zorundadır. Davet edilen üyenin her mektubu postaya verildiğinde, davet eden üye ayrı bir kurala göre (yüzde veya sabit, admin → MektupPuan → Referans kazancı) puan kazanır. Bu puan da ödeme anında sabitlenir; davet eden hesabı engellenmişse puan verilmez.
- Bakiye hiçbir zaman eksiye düşmez; her hareket `PointTransactions` tablosunda kayıtlıdır. Puan hesabındaki `RowVersion`, aynı bakiyenin eşzamanlı iki ödemede harcanmasını engeller.

Listeleme ekranları aggregate yüklemeden doğrudan okuma modeline projekte eden `IAdminQueries` ile çalışır (CQRS-lite); durum değiştiren her işlem ise aggregate metotlarından geçer.

### PayTR ödeme entegrasyonu

[PayTR iFrame API](https://dev.paytr.com/iframe-api) kullanılır. Ödeme asenkrondur: kesin sonuç, müşterinin döndüğü sayfadan değil PayTR'nin sunucuya gönderdiği bildirimden gelir.

1. **Başlatma** (`PaymentService.StartAsync`): tutar, kullanılan / kazanılacak puan ve referans puanı bir `Payment` kaydında sabitlenir; puan ayrılır; mektup **Ödeme Bekleniyor** durumuna geçip kilitlenir; PayTR `get-token` ile iFrame açılır. Tutarın tamamı puanla ödeniyorsa PayTR'ye gidilmez.
2. **Bildirim** (`POST /odeme/paytr/bildirim`): hash doğrulanır; başarılıysa mektup ödenir, başarısızsa taslağa döner ve ayrılan puan iade edilir. Aynı bildirim tekrar gelirse sadece `OK` dönülür.
3. **Vazgeçme / süre dolumu:** müşteri vazgeçebilir; sonucu gelmeyen denemeler `Payment:ExpireAfterMinutes` sonra arka planda kapatılır (puan iade). Kapanmış denemeye sonradan "başarılı" bildirimi gelirse tutar otomatik iade edilir.
4. **İade:** ödenmiş mektup iptal edilince PayTR `odeme/iade` ile kart iadesi yapılır; başarısız olursa admin sipariş detayında "elle iade edin" uyarısı çıkar.

`Payment:Provider = "Simulation"` (varsayılan) iken PayTR yerine uygulamanın simülasyon sayfası açılır, "Ödeme Başarılı / Kart Reddedildi" seçilerek bildirim taklit edilir. Dışarıdan gelen bildirimler simülasyonda kabul edilmez.

**Canlıya geçiş:**
1. PayTR mağaza başvurusu → Mağaza Paneli → Destek & Kurulum → Entegrasyon Bilgileri'nden Mağaza No / Key / Salt'ı alın; user-secrets veya ortam değişkeniyle verin (`Payment__PayTR__MerchantId`, `Payment__PayTR__MerchantKey`, `Payment__PayTR__MerchantSalt`).
2. Mağaza Paneli → Ayarlar'da **Bildirim URL**'yi `https://<alan-adınız>/odeme/paytr/bildirim` yapın (sitenin herkese açık ve HTTPS olması gerekir; localhost'a bildirim gelmez).
3. `Payment:Provider = "PayTR"`, `PayTR:TestMode = true` ile PayTR test kartlarıyla deneyin; işlemin panelde "Başarılı" göründüğünü kontrol edin. Sonra `TestMode = false`, `DebugOn = false` yapın.
4. Uygulama bir proxy (Nginx, yük dengeleyici) arkasındaysa proxy adresini `ForwardedHeadersOptions.KnownProxies`'e ekleyin; PayTR müşterinin gerçek IP'sini (`user_ip`) ister.
5. PayTR iFrame kart ödemesi içindir; PayTR açıkken "Mobil Ödeme" seçeneği gizlenir.

### PTT entegrasyonu

PTT'nin SOAP web servisleri kullanılır (`pttws.ptt.gov.tr`); istekler PTT'nin yayınladığı WSDL'lere göre yazılmış ve testlerde bu WSDL şemalarına karşı doğrulanır (`tests/MektupApp.Infrastructure.Tests/Fixtures`).

| İşlem | Servis / metot | Uygulamada |
|---|---|---|
| Gönderi kaydı | `PttVeriYukleme[Test]` → `kabulEkle2` | Admin → sipariş → "PTT'ye Kaydet ve Postaya Ver" (tek tek veya "Basımda" sekmesinde toplu) |
| Gönderi takip | `GonderiTakipV2[Test]` → `gonderiSorgu2` | Arka plan servisi (`Ptt:Sync`) + sipariş detayında "PTT'den Güncelle" |

- **DDD yapısı:** `IPostalCarrier` (port, Application) → `PttPostalCarrier` / `SimulatedPostalCarrier` (adapter, Infrastructure). İş kuralları `Letter` içinde: `Ship(barkod, "PTT", dosyaAdi)`, `RecordCarrierUpdate(...)`, `MarkDelivered(...)`.
- **Barkod:** PTT'nin tahsis ettiği aralıktan (`Ptt:Barcode`) veritabanındaki sayaçla sırayla üretilir; S10 kontrol hanesi hesaplanır (ör. `RR100000003TR`). Aralık dolunca admin'e uyarı verilir.
- **Takip:** Kargodaki mektupların PTT hareketleri durum geçmişine ve takip sayfasına yazılır; PTT "teslim edildi" dediğinde (`TESALAN` dolu veya son işlem `TESLİM EDİLDİ`) mektup otomatik kapanır, puan ve SMS bu sırada tetiklenir.
- **Simülasyon:** `Ptt:Enabled = false` iken (varsayılan) gerçek gönderi yapılmaz; hareketler zamanla ilerler (geliştirmede 30 sn'de bir).

**Canlıya geçiş:**
1. PTT ile kurumsal sözleşme yapın; müşteri no, şifre ve barkod aralığı alın (entegrasyon@ptt.gov.tr).
2. Bilgileri gizli tutun: `dotnet user-secrets` veya ortam değişkeni (`Ptt__CustomerId`, `Ptt__Password`, gerekirse `Ptt__TrackingUsername` / `Ptt__TrackingPassword`).
3. `Ptt:Barcode` aralığını, `Ptt:ShipmentType` (gonderiTur), `Ptt:ShipmentKind` (gonderiTip), `Ptt:PaymentMethod` (odemesekli) ve `Ptt:ExtraService` (ekhizmet) değerlerini PTT'nin size verdiği kodlarla güncelleyin. Varsayılanlar (`KARGO` / `NORMAL` / `MH`) yer tutucudur, taahhütlü mektup için farklı olabilir.
4. `Ptt:Enabled = true`, `Ptt:UseTestEnvironment = true` ile test gönderisi oluşturun; PTT'nin onay sürecini tamamlayın, ardından `UseTestEnvironment = false` yapın.

### Migration

Model değiştiğinde yeni migration ekleyin (EF aracı `dotnet-tools.json` ile projeye tanımlı):

```bash
dotnet tool restore
dotnet ef migrations add <Isim> --project src/MektupApp.Infrastructure --startup-project src/MektupApp.Web --output-dir Persistence/Migrations
```

Testler:

```bash
dotnet test
```

## Katmanlar

```
src/
  MektupApp.Domain          İş kuralları. Hiçbir katmana bağımlı değil.
    Letters/                Letter (aggregate root), LetterPhoto, LetterStatusChange
                            Value object'ler: PostalAddress, Recipient, LetterStyle, Postcard, TrackingCode, Money
                            Domain servisi: LetterPricingPolicy  ·  Domain event'ler: LetterPaid, LetterShipped...
    Customers/              Customer (aggregate root), Email, PhoneNumber
  MektupApp.Application     Use case'ler (LetterService, FulfillmentService, AccountService), DTO'lar,
                            port'lar (IPaymentGateway, ISmsSender, IFileStorage, IUnitOfWork), event handler'lar
  MektupApp.Infrastructure  EF Core DbContext + mapping + migration'lar (SQL Server), repository'ler, domain event dispatcher,
                            sahte ödeme / SMS, yerel dosya deposu
  MektupApp.Web             MVC: Controller'lar, ViewModel'ler, Razor View'lar
tests/
  MektupApp.Domain.Tests    Aggregate ve fiyatlandırma kuralları için birim testleri
```

Bağımlılık yönü: `Web → Application → Domain`, `Infrastructure → Application/Domain`. Domain hiçbir şeye bağımlı değildir.

### Öne çıkan DDD kararları

- **Kurallar aggregate içinde:** "Ödenen mektup düzenlenemez", "postaya verilen mektubun adresi değişmez", "en fazla 10 fotoğraf", "askere giden mektupta birlik bilgisi zorunlu" gibi kurallar `Letter` / `Recipient` içinde; controller veya servis bunları tekrar kontrol etmez.
- **Durum makinesi:** `Taslak → Ödendi → Basımda → Kargoda → Teslim Edildi` (+ `İptal`). Her geçiş bir metot (`MarkPaid`, `StartProduction`, `Ship`, `MarkDelivered`, `Cancel`) ve geçmişe kaydedilir.
- **Value object'ler** EF Core complex type olarak aynı tabloda kolon şeklinde saklanır.
- **Domain event'ler** `SaveChanges` sonrasında yayınlanır; SMS bildirimleri bu sayede ana iş akışından ayrıdır.
- **Fiyatlandırma** bir domain servisidir; fiyat listesi `appsettings.json` → `Pricing` bölümünden okunur.

## Canlıya almadan önce

- `FakePaymentGateway` → gerçek ödeme sağlayıcısı (iyzico, PayTR vb., 3D Secure).
- `LoggingSmsSender` → gerçek SMS sağlayıcısı.
- Açılışta otomatik migration yerine, yayın sürecinde `dotnet ef database update` veya migration script'i kullanın.
- Üyelik sözleşmesi, mesafeli satış sözleşmesi, KVKK metinleri (şu an yer tutucu).
- Şifre sıfırlama, e-posta doğrulama.
