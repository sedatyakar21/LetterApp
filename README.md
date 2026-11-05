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
- 1 puan = 1 ₺. Müşteri ödeme adımında bakiyesini kullanabilir; tutarın tamamı puanla karşılanırsa ödeme sağlayıcısına gidilmez. Yüzdesel kazanım sadece kart / mobil ile ödenen kısım üzerinden hesaplanır.
- İptal edilen siparişte kullanılan puan otomatik iade edilir (`LetterCancelledAfterPayment` olayı).
- Bakiye hiçbir zaman eksiye düşmez; her hareket `PointTransactions` tablosunda kayıtlıdır. Puan hesabındaki `RowVersion`, aynı bakiyenin eşzamanlı iki ödemede harcanmasını engeller.

Listeleme ekranları aggregate yüklemeden doğrudan okuma modeline projekte eden `IAdminQueries` ile çalışır (CQRS-lite); durum değiştiren her işlem ise aggregate metotlarından geçer.

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
