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

### Şifremi unuttum (`/sifremi-unuttum`)

Giriş sayfasındaki **Şifremi unuttum** bağlantısıyla üye, kayıtlı e-postasını girip yöntemi seçer. İki yöntem **Yönetim → Bildirimler → 🔑 Şifremi unuttum** kartından ayrı ayrı açılıp kapatılır (varsayılan: e-posta açık, SMS kapalı; ikisi de kapalıysa bağlantı görünmez):

- **E-posta:** `/sifre-sifirla?token=...` bağlantısı gider; 60 dakika geçerli, tek kullanımlık. Bağlantı adresi `Notifications:SiteUrl`'den üretilir (isteğin Host başlığına güvenilmez).
- **SMS:** kayıtlı cep telefonuna 6 haneli kod gider; 10 dakika geçerli, en çok 5 yanlış deneme.

Güvenlik: bağlantı anahtarı / kod veritabanında yalnızca SHA-256 özeti olarak tutulur ve bildirim geçmişine yazılmaz (doğrudan gönderilir); yeni talep öncekileri iptal eder; üye başına saatte 3 talep, IP başına 15 dakikada 10 istek sınırı vardır; kayıtlı olmayan adres için de aynı mesaj gösterilir. Şifre değişince üyenin **tüm açık oturumları** (site çerezi ve mobil token'lar) kapanır. Mobil uygulama aynı akışı `/api/mobile/auth/password-reset/{options,request,confirm}` ile kullanır; e-posta bağlantısı sitede açılır. Simülasyon modunda bağlantı / kod log'a yazılır.

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

### Adres formu: il / ilçe listeleri

Mektup formunda (alıcı ve gönderen) ve "adres değiştir" ekranında il ve ilçe açılır listeden seçilir; yeni mektupta alıcı türü varsayılan olarak **Cezaevi** gelir.

- Veri: `src/MektupApp.Web/wwwroot/data/il-ilce.json` (81 il, 973 ilçe). Liste Türkçe alfabeye göre sıralanır (`TurkeyLocations`); mobil uygulama aynı veriyi `/api/mobile/locations` üzerinden alır.
- Site: listeler sunucuda doldurulur, il değişince ilçeler `wwwroot/js/address-picker.js` ile yenilenir. Mobilde il ve ilçe aramalı seçim ekranından seçilir.
- Eski kayıtlar: Türkçe karaktersiz yazılmış değerler listedeki yazıma eşlenir ("Istanbul / Kadikoy" → "İstanbul / Kadıköy"). Listede hiç olmayan bir değer silinmez, seçili ek seçenek olarak kalır.
- Yeni ilçe kurulursa yalnızca JSON dosyasını güncellemek yeterlidir.

### Cezaevi seçimi

Mektup formunda alıcı türü **Cezaevi** iken (yeni mektupta varsayılan) il listesinin adı "Alıcı Cezaevi" olur. İl seçilince o ilin cezaevleri ayrı bir listede gösterilir. Kurum seçilince şunlar otomatik dolar:
- ilçe,
- adres,
- adreste geçiyorsa gizli posta kodu,
- kurum adı (gizli alanda saklanır; zarfa ve PTT kaydına basılır).

Kurum seçiliyken ilçe ve adres **düzenlenemez**. Listede olmayan kurumlar için "Listede yok, adresi elle gireceğim" seçeneği vardır; seçilince alanlar açılır. Aynı seçim mektup detayındaki "adres değiştir" ekranında ve mobil uygulamada da vardır.

- **Alıcı bilgileri:**
  - Cezaevi alıcısında **baba adı** ve **koğuş numarası** isteğe bağlıdır ama formda tavsiye notuyla istenir (`Recipient.FatherName`, `Recipient.Ward`; migration: `RecipientFatherNameWard`).
  - Girilenler zarfta, baskı sayfasında ve PTT kaydında "Baba adı: … · Koğuş: …" olarak yazılır; boşsa satır hiç yazılmaz.
  - Asker alıcısında birlik bilgisi zorunludur.
- **Önceki mektuptan doldurma:** Yeni mektupta alıcı türü cezaevi veya asker iken, müşterinin o türdeki son mektubunun alıcı ve gönderen bilgileri forma hazır gelir; adres ve kurum da buna dahildir. Tür değiştirilince o türün son mektubu yüklenir. Kullanıcı alıcı adını kendisi değiştirdiyse form ezilmez. Mobil: `GET /api/mobile/letters/prefill?type=Inmate`.

- **Veri:** `Prisons` tablosu (migration: `Prisons`), yönetim → 🏛️ **Cezaevleri** ekranından eklenir, düzenlenir, pasifleştirilir. Pasif kurumlar formda listelenmez.
- **Başlangıç verisi:** `src/MektupApp.Infrastructure/Seed/cezaevleri.json`. Tablo boşsa açılışta bir kez yüklenir.
  - Kaynak: Ceza ve Tevkifevleri Genel Müdürlüğü sitesi (`cte.adalet.gov.tr`, harita sayfasındaki il bazlı kurum bilgileri), Kasım 2026.
  - İçerik: 80 ilde 465 kurum. Siirt için sitede kayıt yok.
  - Eksikler: 22 kurumun kaynakta adresi yok; 6 kurumun ilçesi belirlenemedi. Panelde "adresi eksik" olarak işaretlenir ve formda adresi doldurmaz.
  - **Yayından önce adresleri kontrol edin;** resmi kaynak eski olabilir.
- **API:** `GET /api/mobile/prisons?city=Ankara` (giriş gerektirmez); hem site formu hem mobil uygulama kullanır.
- **Form sırası:** "Mektup Yaz"da ilk adım mektup metnidir (sitede ve mobilde): Mektup → Alıcı → Gönderen → Görünüm.

### Belge ekleme

Dilekçe, vekaletname, okul belgesi gibi evraklar mektuba eklenir; basılıp mektupla aynı zarfa konur.

- **Nereden:** mektup detay sayfasındaki "Belgeler" kartı (sitede ve mobilde). Mobilde dosya seçilir veya kâğıt belgenin fotoğrafı çekilir.
- **Kurallar:** PDF, JPG veya PNG; en fazla 10 MB; mektup başına en fazla 20 belge (`Letter.MaxDocuments`). Yalnızca taslakta eklenip kaldırılabilir.
- **Güvenlik:** dosyanın ilk baytları uzantısıyla karşılaştırılır (`DocumentSignature`); adı .pdf yapılmış HTML veya program dosyası reddedilir. Dosya `nosniff` ile, yalnızca sahibine ve yöneticiye sunulur.
- **Ücret:** belge başı, varsayılan 25 ₺ (`Pricing:Document`); yönetim panelindeki Fiyatlar sayfasından değiştirilir. Ödeme özetinde "Belge × n" satırı olarak görünür.
- **Basım:** yönetici sipariş detayında belgeleri listeler; baskı sayfası aşağıda.

### Sipariş yazdırma (`/yonetim/Orders/Print`)

- **Nereden:** sipariş detayındaki "🖨️ Yazdır", sipariş listesinde her satırdaki 🖨️ veya listede seçilenler için "Seçilenleri: 🖨️ Yazdır" (her sekmede; en fazla 50 sipariş).
- **Her sipariş için sırayla:** zarf (ekranda "zarfa konacaklar" kontrol listesiyle) → mektup → fotoğraflar → belgeler. Her bölüm yeni sayfadan başlar.
- **Fotoğraflar:** 10×15 cm fotoğraf kağıdına, kenarsız basılır; dikey fotoğraf 100×150 mm, yatay fotoğraf 150×100 mm sayfaya. Fotoğraf kağıdı tamamen doldurur (oran korunur, 2:3 olmayan fotoğrafların taşan kenarı kırpılır).
- **PDF belgeler:** tarayıcıda pdf.js (`wwwroot/lib/pdfjs`, 3.11.174) ile her sayfası görüntüye çevrilip baskıya eklenir; hepsi hazır olunca "Yazdır" düğmesi açılır. Kötü niyetli PDF'lere karşı `isEvalSupported: false` ile çalışır (CVE-2024-4367). Hazırlanamayan PDF için bağlantı ve "ayrıca yazdırın" uyarısı çıkar.
- **Düğmeler:** "📮 Zarf Yazdır" (yanında zarf boyutu), "✉️ Mektup Yazdır", "📷 Fotoğrafları Yazdır", "📄 Belgeleri Yazdır" yalnızca o bölümü, "🖨️ Tümünü Yazdır" hepsini basar. Yazdırılacak öğesi olmayan düğme kapalıdır.
- **Renkli kâğıt:** arka plan rengi basılmaz; yazıcıya o renkte kâğıt konur (kontrol listesinde yazar).
- **Zarf çıktısı:** varsayılan 13×18 cm (yatay 180×130 mm; DL, C5, C4 de seçilebilir). Sol üst gönderen; sağ üst tahmini ağırlık, altında takip numarasının Code 128 barkodu (`Code128`, dış kütüphane yok) ve numara; sağ alt alıcı; sol alt "Aracı Firma : BiMektup.net" ve logo. Takip numarası PTT kaydında (postaya verirken) oluştuğu için ondan önce barkod basılmaz, ekranda uyarı çıkar.

### Ağırlıklar (`/yonetim/Weights`)

- Yönetim panelinde **⚖️ Ağırlıklar**: A4 mektup kağıdı (yaprak başı), fotoğraf kağıdı (fotoğraf başı), kartpostal, normal belge kağıdı (yaprak başı) ve boş zarf için gram değerleri girilir. Kayıt yoksa varsayılanlar kullanılır (5 / 12 / 5 / 5 / 6 g).
- **Tahmini ağırlık** = zarf + mektup sayfası × A4 + fotoğraf × fotoğraf kağıdı + kartpostal + belge yaprakları × belge kağıdı; yukarı yuvarlanır (`LetterWeightPolicy`).
- Belgelerin yaprak sayısı yüklenirken okunur: görsel 1, PDF'in sayfa sayısı (`PdfPageCounter`; sıkıştırılmış nesne akışları da açılır, okunamazsa 1). Bu özellikten önce yüklenen belgeler 1 yaprak sayılır.
- Zarf çıktısının sağ üstüne basılır; ekrandaki kontrol listesinde kalem kalem dökümü görünür. **PTT kaydına giden ağırlık da aynı hesaptır.**
- Veritabanı: `WeightSettings` tablosu, `LetterDocuments.PageCount` (migration: `WeightSettings`).

### Kokular (`/yonetim/Scents`)

- Yönetim panelinde **🌸 Kokular**: koku adı, sıra ve aktif / pasif girilir (aynı ad iki kez eklenemez). Mektup formundaki (site ve mobil) "Koku" listesi buradan gelir; "Kokusuz" her zaman ilk seçenektir. SSS'deki fiyat satırı da aktif kokuları listeler.
- Fiyat tüm kokular için aynıdır: **Fiyatlar → Koku**. "Ücretsiz koku" kampanyası bu ücreti düşer.
- Mektupta kokunun kimliği ve adı anlık kopya olarak saklanır (`Letters.ScentId`, `ScentName`); ad sonradan değişse de siparişte yazan değişmez. Seçili koku pasife alınırsa o mektup düzenlenirken seçim korunur, yeni mektupta seçilemez. Mektupta seçilmiş koku silinemez, pasife alınır.
- Önceden koda gömülü Manolya, Lavanta ve Bahar Çiçekleri listeye hazır eklenir ve eski siparişler bunlara bağlanır (migration: `ScentCatalog`).

### Kartpostallar (`/yonetim/Postcards`)

- Yönetim panelinde **🖼️ Kartpostallar**: ön yüz görseli (JPG / PNG / WEBP, en fazla 5 MB; dosya imzası kontrol edilir), ad, kategori, sıra ve aktif / pasif girilir. Kategori serbest metindir (mevcutlar önerilir); müşteri ekranında kartpostallar kategoriye göre gruplanır.
- Müşteri (site ve mobil uygulama) mektup yazarken **aktif** kartpostallardan **birden fazlasını** seçebilir (en çok 5, her biri bir kez); hiç aktif kartpostal yoksa bölüm gösterilmez. Fiyat kartpostal başınadır: **Fiyatlar → Kartpostal** × adet; ağırlık da adet kadar eklenir. "Ücretsiz kartpostal" kampanyası bir kartpostalın ücretini düşer.
- Mektupta her kartpostalın kimliği ile adı ve kategorisi **anlık kopya** olarak saklanır (`LetterPostcards` tablosu): katalog değişse de siparişte hangi kartpostalların konacağı bilinir. Seçili kartpostal sonradan pasife alınırsa o mektup düzenlenirken seçim korunur, yeni mektupta seçilemez.
- Herhangi bir mektupta seçilmiş kartpostal **silinemez** (siparişte görseli gerekir), pasife alınır. Sipariş detayında ve baskı sayfasının zarflama listesinde kartpostalın adı ve küçük görseli görünür.
- Görseller `/kartpostal/{id}/gorsel` adresinden herkese açık sunulur (mobil uygulama `config` yanıtındaki `postcards` listesini kullanır).
- Önceki sabit 5 kategori × 4 renkli tasarım kaldırıldı; eski siparişler "Aşk #2" gibi okunur bir adla korunur (migration'lar: `PostcardCatalog`, `MultiplePostcards`).
- **Veritabanı:** `LetterDocuments` tablosu ve `PricingSettings.Document` sütunu (migration: `LetterDocuments`).

### Mobil uygulama (Flutter · iOS ve Android)

`mobile/` klasöründeki Flutter uygulaması, sitenin üye tarafındaki işlevlerini telefona taşır.

- **Ekranlar:**
  - Giriş ve kayıt.
  - **Mektuplarım:** liste, detay, durum geçmişi, takip no, iptal, adres değişikliği ve taslak silme.
  - **Mektup Yaz:** 5 adım. Alıcı, kâğıt renginde canlı önizlemeli mektup, görünüm ve ekstralar, gönderen, tahmini tutarlı özet.
  - **Fotoğraf:** galeriden veya kameradan eklenir.
  - **Belge:** dosyalardan (PDF, JPG, PNG) seçilir veya kamerayla çekilir. Görsel belgeler uygulamada önizlenir; PDF'ler yalnızca listelenir.
  - **Ödeme:** kart veya mobil ödeme, MektupPuan, kupon kodu, kampanya indirimi.
  - **Kampanyalar**, **Gönderi takibi**, teslim edilen mektuba **yorum**.
  - **Hesabım:** MektupPuan hareketleri, referans no, doğum tarihi, WhatsApp destek.
- **API:** `src/MektupApp.Web/Api` altında `/api/mobile/...` uç noktaları bulunur ve JSON döner. Site ve mobil aynı servisleri ve iş kurallarını kullanır.
- **Kimlik doğrulama:**
  - Giriş ve kayıt; 1 saatlik erişim token'ı ve 30 günlük yenileme token'ı verir (ASP.NET Core BearerToken).
  - Uygulama token'ları cihazın güvenli deposunda (Keychain / Keystore) saklar ve süresi dolunca kendisi yeniler.
  - Engellenen hesabın token'ı her istekte reddedilir.
  - API çerez kullanmaz, bu yüzden CSRF riski yoktur.
- **Ödeme:**
  - PayTR formu uygulama içi tarayıcıda açılır. Form bitince `/mobil/odeme-sonuc` adresine dönülür; uygulama bu adresi yakalayıp tarayıcıyı kapatır ve kesin sonucu sunucudan sorgular.
  - Simülasyon modunda uygulama kendi "Test ödemesi" ekranını gösterir.
- **Google ile giriş:** mobilde henüz yok. Google hesabıyla üye olanlar şimdilik siteyi kullanır.

**Çalıştırma:**

```bash
cd mobile
flutter run
```

API adresi varsayılan olarak geliştirme sunucusudur:
- Android emülatörü: `http://10.0.2.2:5213`
- iOS simülatörü / web: `http://localhost:5213`

Canlı sunucu için derlerken adres verilir:

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://bimektup.net
```

```bash
flutter build ipa --release --dart-define=API_BASE_URL=https://bimektup.net
```

**Notlar:**
- iOS derlemesi için macOS ve Xcode gerekir.
- Mağazaya göndermeden önce şunlar ayarlanmalıdır:
  - Uygulama kimliği `net.bimektup.bimektup` (Android `build.gradle.kts`, iOS Xcode projesi).
  - Uygulama simgesi.
  - Android imza anahtarı.
- Şifresiz HTTP yalnızca Android debug sürümünde ve iOS'ta yerel ağ için açıktır. Canlı API HTTPS olmalıdır.
- Web önizlemesinde (`flutter run -d web-server`) API çağrıları için CORS yalnızca geliştirme ortamında ve localhost kökenlerine açıktır.

Testler:

```bash
cd mobile
flutter test
```

### Havale / EFT ile ödeme

Yönetim panelindeki **🏦 Havale / EFT** sayfasından açılıp kapatılır; indirim oranı (%0–50), banka adı, hesap sahibi, IBAN (TR biçimi ve mod-97 kontrol hanesi doğrulanır), müşteriye not ve bekleme süresi (1–30 gün) buradan girilir.

- **Müşteri:** Ödeme sekmesinde "Havale / EFT"yi seçince indirim satırı eklenir, sağda hesap bilgileri ve **Havale ile Siparişi Oluştur** düğmesi çıkar. İndirim kampanyadan sonra, MektupPuan'dan önce uygulanır ve `Payment.BankTransferDiscount`'ta sabitlenir. Sipariş **Ödeme Bekleniyor**'a geçer; bekleme sayfasında (ve mobilde mektup ekranında) IBAN, tutar, açıklamaya yazılacak sipariş numarası ve son gün gösterilir.
- **Yönetici:** Bekleyen havaleler ayar sayfasının sağında listelenir; **Siparişler → Detay**'da "Havale alındı" mektubu ödenmiş yapar, "Gelmedi" taslağa döndürür (ayrılan puan iade edilir).
- **Süre dolumu:** onaylanmayan havaleler bekleme süresi sonunda arka planda kapatılır ve mektup taslağa döner.

### PayTR ödeme entegrasyonu

[PayTR iFrame API](https://dev.paytr.com/iframe-api) kullanılır. Ödeme asenkrondur: kesin sonuç, müşterinin döndüğü sayfadan değil PayTR'nin sunucuya gönderdiği bildirimden gelir.

1. **Başlatma** (`PaymentService.StartAsync`): tutar, kullanılan / kazanılacak puan ve referans puanı bir `Payment` kaydında sabitlenir; puan ayrılır; mektup **Ödeme Bekleniyor** durumuna geçip kilitlenir; PayTR `get-token` ile iFrame açılır. Tutarın tamamı puanla ödeniyorsa PayTR'ye gidilmez.
2. **Bildirim** (`POST /odeme/paytr/bildirim`): hash doğrulanır; başarılıysa mektup ödenir, başarısızsa taslağa döner ve ayrılan puan iade edilir. Aynı bildirim tekrar gelirse sadece `OK` dönülür.
3. **Vazgeçme / süre dolumu:** müşteri vazgeçebilir; sonucu gelmeyen denemeler `Payment:ExpireAfterMinutes` sonra arka planda kapatılır (puan iade). Kapanmış denemeye sonradan "başarılı" bildirimi gelirse tutar otomatik iade edilir.
4. **İade:** ödenmiş mektup iptal edilince PayTR `odeme/iade` ile kart iadesi yapılır; başarısız olursa admin sipariş detayında "elle iade edin" uyarısı çıkar.

**Mektup Yaz → Ödeme sekmesi (site ve mobil):** solda kesin ödeme kalemleri, kampanya kuponu ve MektupPuan (`GET /mektup/{id}/odeme/teklif`), sağda PayTR'nin güvenli kart formu sayfa içinde gömülü (iFrame; mobilde uygulama içi WebView). Sekme açılınca taslak kaydedilip ödeme başlatılır; kupon / puan / yöntem değişince deneme kapatılıp yenisi başlar; geri dönülürse (onayla) deneme kapatılır ve mektup yeniden düzenlenebilir. Ödeme sonrası PayTR'nin açtığı dönüş adresi (`…/odeme/sonuc?cerceve=1`) çerçeveden çıkıp sonucu sayfanın tamamında gösterir. Kart bilgisi sunucumuza gelmez. Eski ayrı ödeme sayfası (`/mektup/{id}/odeme`) bekleyen ödemeler için kullanılmaya devam eder.

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
  MektupApp.Web             MVC: Controller'lar, ViewModel'ler, Razor View'lar; Api/ altında mobil JSON API
mobile/                     Flutter mobil uygulaması (iOS + Android)
tests/
  MektupApp.Domain.Tests    Aggregate ve fiyatlandırma kuralları için birim testleri
```

Bağımlılık yönü: `Web → Application → Domain`, `Infrastructure → Application/Domain`. Domain hiçbir şeye bağımlı değildir.

### Öne çıkan DDD kararları

- **Kurallar aggregate içinde:** "Ödenen mektup düzenlenemez", "postaya verilen mektubun adresi değişmez", "en fazla 30 fotoğraf", "askere giden mektupta birlik bilgisi zorunlu" gibi kurallar `Letter` / `Recipient` içinde; controller veya servis bunları tekrar kontrol etmez.
- **Durum makinesi:** `Taslak → Ödendi → Basımda → Kargoda → Teslim Edildi` (+ `İptal`). Her geçiş bir metot (`MarkPaid`, `StartProduction`, `Ship`, `MarkDelivered`, `Cancel`) ve geçmişe kaydedilir.
- **Value object'ler** EF Core complex type olarak aynı tabloda kolon şeklinde saklanır.
- **Domain event'ler** `SaveChanges` sonrasında yayınlanır; SMS bildirimleri bu sayede ana iş akışından ayrıdır.
- **Fiyatlandırma** bir domain servisidir; fiyat listesi `appsettings.json` → `Pricing` bölümünden okunur.

## Canlıya almadan önce

- `FakePaymentGateway` → gerçek ödeme sağlayıcısı (iyzico, PayTR vb., 3D Secure).
- `LoggingSmsSender` → gerçek SMS sağlayıcısı.
- Açılışta otomatik migration yerine, yayın sürecinde `dotnet ef database update` veya migration script'i kullanın.
- Üyelik sözleşmesi, mesafeli satış sözleşmesi, KVKK metinleri (şu an yer tutucu).
- E-posta doğrulama.
- Uygulama Nginx / yük dengeleyici arkasındaysa proxy adresini `ForwardedHeadersOptions.KnownProxies`'e ekleyin; aksi halde şifremi unuttum hız sınırı (IP başına) tüm kullanıcılar için ortak sayılır.
