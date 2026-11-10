# BiMektup (BiMektup.net)

Kod tabanı ve projeler `MektupApp.*` adını korur; kullanıcıya görünen marka **BiMektup**'tur.

**Tasarım:** Renkler logodan alınmıştır: lacivert `#26304E`, bordo `#8E2235`, kalp kırmızısı `#A8263A`, krem kağıt `#F7F6F0`. Başlık ve marka yazısında Lora serif kullanılır. Değişkenler `wwwroot/css/site.css` dosyasının başında tanımlıdır. Logo dosyaları `wwwroot/img/` klasöründedir: arka planı kaldırılmış `bimektup-logo.png` ve küçük işaret `bimektup-mark.svg`; favicon `wwwroot/favicon.svg`'dir.

**Blog:** `/blog` (liste) ve `/blog/{adres}` (yazı). Yazılar admin → 📝 Blog'dan eklenir: Markdown içerik, canlı önizleme, kapak görseli, taslak / yayın. Yazı adresi başlıktan Türkçe karakterler sadeleştirilerek üretilir, çakışırsa sonuna `-2` eklenir. Taslaklar ziyaretçiye 404 döner; yönetici `?onizleme=1` ile görebilir. Güvenlik için Markdown içinde ham HTML kapalıdır, bağlantılarda sadece http(s), mailto ve site içi adreslere izin verilir (`MarkdownRenderer`, testleri `tests/MektupApp.Web.Tests`). Ana sayfada son 3 yazı gösterilir.

**WhatsApp yardım menüsü:** Sitenin sağ altındaki "Yardım" butonu hazır konularla (sipariş takibi, ödeme, adres değişikliği, puan…) WhatsApp'ı önceden yazılmış mesajla açar. Mektup detay sayfasında mesaja takip kodu, giriş yapılmışsa ad soyad eklenir. Numara `appsettings.json` → `Support:WhatsAppNumber` (ülke koduyla, ör. `905321234567`); boşsa buton gösterilmez. Şu anki destek hattı: 0543 427 77 91.

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
- **Kampanyalar:** duyuru sayfası (`/kampanyalar`). Başlık, kısa açıklama, Markdown koşullar, rozet, isteğe bağlı kupon kodu, başlangıç / bitiş tarihi (bitiş günü dahil, Türkiye saati) ve kapak görseli. Durum tarihlere göre kendiliğinden belirlenir (Yakında / Devam ediyor / Son N gün / Sona erdi); aktif kampanyalar ana sayfadaki şeritte görünür.
  - **İndirim:** her kampanyaya bir indirim türü verilir: sadece duyuru, yüzde, sabit tutar (₺), ücretsiz koku veya ücretsiz kartpostal. İsteğe bağlı olarak en düşük sepet tutarı da tanımlanabilir.
  - **Ödeme ekranında uygulanma:** kupon kodu olmayan kampanya otomatik uygulanır; kuponlu kampanya, kod "Kupon kodu" alanına girildiğinde uygulanır.
  - **Birleşmeme kuralı:** kampanyalar birleşmez. Siparişe, koşulları sağlayanlar arasından müşteriye en çok indirim sağlayan tek kampanya uygulanır.
  - **Puanlarla ilişkisi:** indirim, MektupPuan kullanımından önce düşülür. Kazanılacak puan ve referans puanı indirimli tutardan hesaplanır.
  - **Kayıt:** uygulanan kampanya ve indirim tutarı, ödeme denemesine ve mektuba o anki haliyle kaydedilir; kampanya sonradan değişse de bu kayıt etkilenmez. Müşteri sipariş detayında ve yönetim sipariş detayında görünür.
  - **Ücretsiz sipariş:** indirim tutarın tamamını karşılarsa ödeme formu açılmaz, sipariş doğrudan tamamlanır.
  - **Hedef kitle:** kampanya herkese, yeni üyelere (üyeliğin ilk N günü) veya doğum günü olanlara (doğum gününden ±N gün; 29 Şubat ve yılbaşı geçişi dahil) uygulanabilir.
  - **Üye başına kullanım sınırı:** örneğin "1 kez". Doğum günü kampanyasında hak her doğum günü yenilenir. Ödenmiş siparişler ve sonucu beklenen ödemeler sayılır; iptal edilen sipariş hakkı geri verir.
  - **Doğum tarihi:** üye olurken isteğe bağlı girilir ya da sonradan **Hesabım** (`/hesabim`) sayfasından eklenir. Üye bir kez girer, sonra değiştiremez; düzeltme ve silme yönetimdeki müşteri detayından yapılır.
- **Yorumlar:** iki tür yorum var; ikisi de yönetici onaylamadan sitede görünmez. Yönetim → 💬 Yorumlar ekranında bekleyen, yayında ve reddedilen yorumlar listelenir. Bekleyen yorum sayısı menüde rozet olarak görünür. Yorum onaylanabilir, nedenli reddedilebilir, yayından kaldırılabilir ya da silinebilir.
  - **Müşteri yorumları (`/yorumlar`):** üye, teslim edilen her mektup için mektup detayından bir kez 1–5 yıldız ve en az 10 karakterlik yorum yazar. Sayfada ortalama puan ve yıldız dağılımı gösterilir; ana sayfada son 3 yorum yer alır. Gizlilik için ad, soyadın baş harfiyle gösterilir ("Ayşe Y.").
  - **Blog yorumları:** üyeler yazının altına yorum yazar ve onay bekleyen yorumlarını yalnızca kendileri görür. Spam önlemi olarak bir yazıda en fazla 3 bekleyen yorum olabilir.

### MektupPuan (sadakat puanı)

- Kazanılacak puan **ödeme anında** o anki ayara göre hesaplanıp mektuba yazılır (ödeme ekranında gösterilir); puan, mektup **postaya verildiğinde** (`LetterShipped` olayı) hesaba geçer. Ödeme sonrası iptal edilen mektup puan kazandırmaz.
- **Puan değeri:** 1 puanın TL karşılığı admin → MektupPuan → "Puan değeri" bölümünden ayarlanır (varsayılan 1 puan = 1 ₺, 0,01–1.000 ₺ arası). Kazanım kuralları puan cinsindendir; değer sadece puan harcanırken TL'ye çevirmede kullanılır. Ödemede kullanılan puan ve TL indirim ayrı ayrı kaydedilir (`PointsUsed`, `PointsDiscount`), iptalde puan olarak iade edilir. Değeri değiştirmek mevcut bakiyelerin TL karşılığını da değiştirir.
- Müşteri ödeme adımında bakiyesini kullanabilir; tutarın tamamı puanla karşılanırsa ödeme sağlayıcısına gidilmez. Yüzdesel kazanım sadece kart / mobil ile ödenen kısım üzerinden hesaplanır.
- İptal edilen siparişte kullanılan puan otomatik iade edilir (`LetterCancelledAfterPayment` olayı).
- **Referans (arkadaş daveti):** her üyenin benzersiz ve akılda kalıcı bir referans no'su vardır: ad-soyad baş harfleri + 6 haneli sayı (ör. "Ayşe Yılmaz" → `AY482915`; Türkçe harfler sadeleştirilir, yazarken boşluk / küçük harf / Türkçe harf fark etmez) ve "Puanlarım" sayfasında davet linkiyle (`/kayit?ref=MK...`) paylaşılır. Kayıtta "Bizi nereden duydunuz?" (Sosyal medya / Radyo / Arkadaş / Diğer) zorunludur; "Arkadaş" seçilince isteğe bağlı referans no alanı açılır, girilirse geçerli olmak zorundadır. Davet edilen üyenin her mektubu postaya verildiğinde, davet eden üye ayrı bir kurala göre (yüzde veya sabit, admin → MektupPuan → Referans kazancı) puan kazanır. Bu puan da ödeme anında sabitlenir; davet eden hesabı engellenmişse puan verilmez.
- Bakiye hiçbir zaman eksiye düşmez; her hareket `PointTransactions` tablosunda kayıtlıdır. Puan hesabındaki `RowVersion`, aynı bakiyenin eşzamanlı iki ödemede harcanmasını engeller.

Listeleme ekranları aggregate yüklemeden doğrudan okuma modeline projekte eden `IAdminQueries` ile çalışır (CQRS-lite); durum değiştiren her işlem ise aggregate metotlarından geçer.

### SMS ve e-posta bildirimleri

**Gönderilen bildirimler:** hoş geldiniz, sipariş alındı, postaya verildi (takip kodu ve bağlantısıyla), teslim edildi (değerlendirme davetiyle), iptal ve iade, MektupPuan kazanımı, referans puanı.

- **Hangi kanaldan:** her olay için SMS ve/veya e-posta, **Yönetim → 🔔 Bildirimler** ekranından seçilir. Varsayılan: hoş geldin yalnızca e-posta, diğerleri ikisi.
- **Kuyruk:** olaylar mesajı hemen göndermez, `Notifications` tablosuna (outbox) yazar. Arka plan servisi 10 saniyede bir gönderir.
- **Hata olursa:** sağlayıcı geçici hata verirse mesaj 1 dk, 5 dk, 30 dk ve 2 saat sonra yeniden denenir; 5 denemeden sonra "Başarısız" olur. Geçersiz numara veya hatalı hesap bilgisi gibi kalıcı hatalarda hemen "Başarısız" olur.
- **Ana işleme etkisi:** bildirim hatası ödeme veya kargo işlemini bozmaz.
- **Yönetim ekranı:** gönderim geçmişi (kanal ve durum filtresi), e-posta önizleme, başarısız mesajı tekrar deneme ve test SMS'i / test e-postası gönderme.
- **İçerik ve güvenlik:** e-postalar markalı HTML ve düz metin olarak gider; kullanıcıdan gelen metinler (ör. alıcı adı) HTML'e kaçışlanır. SMS'lerde Türkçe karakter kullanılır; tutarlar "TL" ile yazılır.
- **Ticari ileti değildir:** bunlar işlem bilgilendirmeleridir. Netgsm'de İYS filtresi `0` (bilgilendirme) ile gönderilir. Kampanya duyurusu gibi ticari SMS / e-posta gönderilecekse İYS onayı gerekir; bu özellik bunu kapsamaz.

**Kurulum (şifreleri `appsettings.json` dosyasına yazmayın):**
- **SMS (Netgsm):** `Notifications:Sms:Netgsm:UserCode`, `Header` (Netgsm'de onaylı gönderici adı) ve `Password`. Üçü de tanımlıysa Netgsm kullanılır, eksikse SMS simülasyonda (log) kalır.
- **E-posta (SMTP):** `Notifications:Email:Smtp:Host`, `Port` (587 / 465), `Security` (`StartTls` / `SslOnConnect`), `UserName`, `Password`, `FromAddress`, `FromName`. `Host` ve `FromAddress` tanımlıysa SMTP kullanılır.
- **Bağlantılar:** `Notifications:SiteUrl` e-posta ve SMS'teki bağlantıların adresidir (canlı: `https://bimektup.net`).

```bash
dotnet user-secrets set "Notifications:Sms:Netgsm:Password" "xxxx" --project src/MektupApp.Web
```

```bash
dotnet user-secrets set "Notifications:Email:Smtp:Password" "xxxx" --project src/MektupApp.Web
```

Canlıda ortam değişkeni adları `Notifications__Sms__Netgsm__Password` ve `Notifications__Email__Smtp__Password` şeklindedir. Netgsm entegrasyonu, Netgsm'in XML POST API'sine (`https://api.netgsm.com.tr/sms/send/xml`) göre yazıldı ve birim testleriyle doğrulandı; gerçek hesapla ilk gönderimde **Test SMS'i gönder** ile denenmelidir.

### Google ile giriş

Giriş ve kayıt sayfalarında "Google ile devam et" butonu vardır.

- **İlk giriş:** Google hesabı hiçbir üyeliğe bağlı değilse ve e-postası kayıtlı değilse, üye kısa bir formla telefon, "bizi nereden duydunuz", isteğe bağlı referans no ve doğum tarihi girip üyeliği tamamlar. E-posta Google'dan gelir.
- **Mevcut hesap:** Google'ın **doğruladığı** e-posta zaten kayıtlıysa, Google o hesaba otomatik bağlanır. Doğrulanmamışsa bağlanmaz; üyeden şifresiyle girip **Hesabım** sayfasından Google'ı bağlaması istenir.
- **Şifresiz hesaplar:** Google ile oluşturulan hesapların şifresi yoktur; şifreyle giriş denemesinde "Google ile devam et" uyarısı çıkar. Bu hesaplarda Google bağlantısı kaldırılamaz.
- **Hesap eşleştirme:** üyeler Google'ın kalıcı kimliğiyle (`sub`) eşleştirilir; Google tarafında e-posta değişse de giriş çalışır.

**Kurulum:**
1. Google Cloud Console → *APIs & Services* → *Credentials* → **OAuth client ID** oluşturun (tür: *Web application*).
2. *Authorized redirect URIs* alanına `https://alanadiniz/signin-google` ekleyin. Geliştirme için `http://localhost:5213/signin-google` de eklenebilir.
3. Anahtarları `appsettings.json` dosyasına **yazmayın**; user-secrets veya ortam değişkeniyle verin:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "xxxx.apps.googleusercontent.com" --project src/MektupApp.Web
```

```bash
dotnet user-secrets set "Authentication:Google:ClientSecret" "xxxx" --project src/MektupApp.Web
```

Canlı ortamda ortam değişkeni adları `Authentication__Google__ClientId` ve `Authentication__Google__ClientSecret` şeklindedir.

**Anahtar yoksa:** geliştirme ortamında Google yerine simülasyon sayfası (`/giris/google/simulasyon`) açılır; buradan ad, e-posta ve "e-posta doğrulandı" bilgisi seçilerek akış denenebilir. Canlı ortamda anahtar yoksa buton hiç gösterilmez ve simülasyon sayfası kapalıdır.

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
