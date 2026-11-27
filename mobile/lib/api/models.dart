// Sunucudan gelen JSON modelleri (alan adları /api/mobile yanıtlarıyla aynıdır).

double _d(dynamic v) => v == null ? 0 : (v as num).toDouble();
String _utc(String s) => RegExp(r'(Z|[+-]\d\d:?\d\d)$').hasMatch(s) ? s : '${s}Z';
double? _dn(dynamic v) => v == null ? null : (v as num).toDouble();
DateTime _dt(dynamic v) => DateTime.parse(v as String).toLocal();
DateTime? _dtn(dynamic v) => v == null ? null : DateTime.parse(v as String).toLocal();
List<T> _list<T>(dynamic v, T Function(Map<String, dynamic>) f) =>
    (v as List? ?? const []).map((e) => f(e as Map<String, dynamic>)).toList();

class Option {
  const Option(this.value, this.label);
  final String value;
  final String label;
  factory Option.fromJson(Map<String, dynamic> j) => Option(j['value'] as String, j['label'] as String);
}

class Prices {
  Prices(this.json);
  final Map<String, dynamic> json;
  double get basePrice => _d(json['basePrice']);
  double get extraPage => _d(json['extraPage']);
  double get photo => _d(json['photo']);
  double get document => _d(json['document']);
  double get postcard => _d(json['postcard']);
  double get coloredPaper => _d(json['coloredPaper']);
  double get coloredEnvelope => _d(json['coloredEnvelope']);
  double get scent => _d(json['scent']);
  double get mobileSurchargeRate => _d(json['mobilePaymentSurchargeRate']);
}

class Support {
  Support(this.whatsAppNumber, this.whatsAppDisplay, this.email, this.hours);
  final String? whatsAppNumber;
  final String? whatsAppDisplay;
  final String? email;
  final String? hours;
}

/// Şifremi unuttum: yönetim panelinden açılan yöntemler.
class PasswordResetOptions {
  PasswordResetOptions(this.emailEnabled, this.smsEnabled, this.codeLength);
  PasswordResetOptions.fromJson(Map<String, dynamic> j)
      : emailEnabled = j['emailEnabled'] as bool? ?? false,
        smsEnabled = j['smsEnabled'] as bool? ?? false,
        codeLength = j['codeLength'] as int? ?? 6;
  final bool emailEnabled;
  final bool smsEnabled;
  final int codeLength;
  bool get anyEnabled => emailEnabled || smsEnabled;
}

/// Birim ağırlıklar (gram). Tahmini ağırlık sunucudaki LetterWeightPolicy ile aynı hesaplanır.
class PaperWeights {
  PaperWeights(this.json);
  final Map<String, dynamic> json;
  double get letterSheet => _d(json['letterSheet']);
  double get photo => _d(json['photo']);
  double get postcard => _d(json['postcard']);
  double get documentSheet => _d(json['documentSheet']);
  double get envelope => _d(json['envelope']);

  /// Zarf + mektup sayfaları + fotoğraflar + kartpostallar + belge yaprakları; yukarı yuvarlanır.
  int estimate({required int letterSheets, required int photos, required int postcards, required int documentSheets}) =>
      (envelope + letterSheets * letterSheet + photos * photo + postcards * postcard + documentSheets * documentSheet - 1e-9)
          .ceil();
}

/// Bir koku (yönetim panelindeki liste). Siparişte koku listeden silinmişse [id] boş olabilir.
class ScentInfo {
  ScentInfo(this.id, this.name);
  ScentInfo.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String?,
        name = j['name'] as String;
  final String? id;
  final String name;
}

/// Katalogdaki bir kartpostal. [imageUrl] sitenin köküne göredir (giriş gerektirmez); eski siparişlerde [id] ve görsel yoktur.
class PostcardInfo {
  PostcardInfo(this.id, this.title, this.category, this.imageUrl);
  PostcardInfo.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String?,
        title = j['title'] as String,
        category = j['category'] as String,
        imageUrl = j['imageUrl'] as String?;
  final String? id;
  final String title, category;
  final String? imageUrl;
}

/// Formdaki seçenekler, fiyatlar ve sınırlar (/api/mobile/config).
class AppSettings {
  AppSettings.fromJson(Map<String, dynamic> j)
      : prices = Prices(j['prices'] as Map<String, dynamic>),
        recipientTypes = _list(j['recipientTypes'], Option.fromJson),
        paperColors = _list(j['paperColors'], Option.fromJson),
        envelopeColors = _list(j['envelopeColors'], Option.fromJson),
        fonts = _list(j['fonts'], Option.fromJson),
        inks = _list(j['inks'], Option.fromJson),
        textSizes = j['textSizes'] == null
            ? const [Option('Small', 'Küçük'), Option('Medium', 'Orta'), Option('Large', 'Büyük')]
            : _list(j['textSizes'], Option.fromJson),
        scents = j['scents'] == null ? const [] : _list(j['scents'], ScentInfo.fromJson),
        postcards = j['postcards'] == null ? const [] : _list(j['postcards'], PostcardInfo.fromJson),
        maxPostcards = j['maxPostcards'] as int? ?? 5,
        weights = j['weights'] == null ? null : PaperWeights(j['weights'] as Map<String, dynamic>),
        acquisitionChannels = _list(j['acquisitionChannels'], Option.fromJson),
        maxPhotos = j['maxPhotos'] as int,
        maxPhotoBytes = j['maxPhotoBytes'] as int,
        maxDocuments = j['maxDocuments'] as int? ?? 20,
        maxDocumentBytes = j['maxDocumentBytes'] as int? ?? 10 * 1024 * 1024,
        maxContentLength = j['maxContentLength'] as int,
        charsPerPage = j['charsPerPage'] as int,
        charsPerLineBreak = j['charsPerLineBreak'] as int,
        minPasswordLength = j['minPasswordLength'] as int,
        loyaltyEnabled = j['loyaltyEnabled'] as bool,
        mobilePaymentAvailable = j['mobilePaymentAvailable'] as bool,
        paymentSimulation = j['paymentSimulation'] as bool,
        support = Support(j['support']['whatsAppNumber'] as String?, j['support']['whatsAppDisplay'] as String?,
            j['support']['email'] as String?, j['support']['hours'] as String?);

  final Prices prices;
  final List<Option> recipientTypes, paperColors, envelopeColors, fonts, inks, acquisitionChannels;

  /// Mektup yazı boyutu seçenekleri (Küçük / Orta / Büyük).
  final List<Option> textSizes;

  /// Yönetim panelinden girilen aktif kokular (sırasıyla); "Kokusuz" listede yoktur, uygulama ekler.
  final List<ScentInfo> scents;

  /// Yönetim panelinden eklenen aktif kartpostallar (sırasıyla).
  final List<PostcardInfo> postcards;

  /// Bir mektuba en çok kaç kartpostal eklenebilir.
  final int maxPostcards;

  /// Zarftaki tahmini ağırlık için birim ağırlıklar (eski sunucuda yok).
  final PaperWeights? weights;
  final int maxPhotos, maxPhotoBytes, maxDocuments, maxDocumentBytes, maxContentLength, charsPerPage, charsPerLineBreak, minPasswordLength;
  final bool loyaltyEnabled, mobilePaymentAvailable, paymentSimulation;
  final Support support;

  /// Sunucudaki sayfa sayısı kuralının aynısı (fiyat tahmini için).
  int pageCount(String content) {
    if (content.isEmpty) return 1;
    final breaks = '\n'.allMatches(content).length;
    final weighted = content.length + breaks * charsPerLineBreak;
    final pages = (weighted / charsPerPage).ceil();
    return pages < 1 ? 1 : pages;
  }

  String label(List<Option> options, String? value) =>
      options.firstWhere((o) => o.value == value, orElse: () => Option(value ?? '', value ?? '')).label;
}

/// Türkiye'nin 81 ili ve ilçeleri (/api/mobile/locations), Türkçe alfabeye göre sıralı.
class Locations {
  Locations(this.provinces) : _byKey = {for (final p in provinces) fold(p.name): p};

  factory Locations.fromJson(List<dynamic> j) => Locations([
        for (final e in j)
          Province(e['name'] as String, (e['districts'] as List).cast<String>()),
      ]);

  static final empty = Locations(const []);

  final List<Province> provinces;
  final Map<String, Province> _byKey;

  List<String> get names => [for (final p in provinces) p.name];

  /// İli bulur; büyük/küçük harf ve Türkçe karakter farkı yok sayılır ("Istanbul" → "İstanbul").
  Province? find(String? city) => city == null || city.trim().isEmpty ? null : _byKey[fold(city)];

  /// İlçenin listedeki yazımı ("Kadikoy" → "Kadıköy"); yoksa null.
  String? findDistrict(Province? p, String? district) {
    if (p == null || district == null || district.trim().isEmpty) return null;
    final key = fold(district);
    for (final d in p.districts) {
      if (fold(d) == key) return d;
    }
    return null;
  }

  /// Karşılaştırma anahtarı: Türkçe kurallarıyla küçük harf + sadeleştirilmiş harfler (sunucudaki Fold ile aynı).
  static String fold(String value) {
    const map = {'ç': 'c', 'ğ': 'g', 'ı': 'i', 'ö': 'o', 'ş': 's', 'ü': 'u', 'â': 'a', 'î': 'i', 'û': 'u'};
    final lower = value.trim().replaceAll('I', 'ı').replaceAll('İ', 'i').toLowerCase();
    final b = StringBuffer();
    for (final ch in lower.split('')) {
      b.write(map[ch] ?? ch);
    }
    return b.toString();
  }
}

/// Cezaevi (alıcı türü "Cezaevi" iken adres otomatik doldurmak için).
class PrisonOption {
  PrisonOption.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        name = j['name'] as String,
        city = j['city'] as String,
        district = j['district'] as String,
        addressLine = j['addressLine'] as String,
        postalCode = j['postalCode'] as String;
  final String id, name, city, district, addressLine, postalCode;
}

class Province {
  const Province(this.name, this.districts);
  final String name;
  final List<String> districts;
}

class Me {
  Me.fromJson(Map<String, dynamic> j)
      : fullName = j['fullName'] as String,
        email = j['email'] as String,
        phone = j['phone'] as String,
        memberSince = DateTime.parse(j['memberSince'] as String),
        birthDate = j['birthDate'] == null ? null : DateTime.parse(j['birthDate'] as String),
        referralCode = j['referralCode'] as String,
        referredCount = j['referredCount'] as int,
        googleLinked = j['googleLinked'] as bool,
        pointsBalance = _d(j['pointsBalance']),
        pointValue = _d(j['pointValue']),
        loyaltyEnabled = j['loyaltyEnabled'] as bool,
        hasPassword = j['hasPassword'] as bool? ?? true,
        pendingChanges = j['pendingChanges'] == null ? const [] : _list(j['pendingChanges'], PendingContactChange.fromJson);

  /// Onay kodu gönderilmiş, onay bekleyen e-posta / telefon değişiklikleri.
  final List<PendingContactChange> pendingChanges;

  PendingContactChange? pending(String kind) {
    for (final p in pendingChanges) {
      if (p.kind == kind) return p;
    }
    return null;
  }

  /// Şifreli hesap mı (Google ile açılan hesaplarda false): bilgi değiştirirken şifre istenir.
  final bool hasPassword;

  final String fullName, email, phone, referralCode;
  final DateTime memberSince;
  final DateTime? birthDate;
  final int referredCount;
  final bool googleLinked, loyaltyEnabled;
  final double pointsBalance, pointValue;

  String get firstName => fullName.split(' ').first;
}

class Address {
  const Address({this.line = '', this.district = '', this.city = '', this.postalCode = ''});
  final String line, district, city, postalCode;

  factory Address.fromJson(Map<String, dynamic> j) => Address(
      line: j['line'] as String,
      district: j['district'] as String,
      city: j['city'] as String,
      postalCode: j['postalCode'] as String);

  Map<String, dynamic> toJson() => {'line': line, 'district': district, 'city': city, 'postalCode': postalCode};

  bool get isEmpty => line.isEmpty && district.isEmpty && city.isEmpty && postalCode.isEmpty;

  @override
  String toString() => postalCode.isEmpty ? '$line, $district / $city' : '$line, $district / $city $postalCode';
}

class StatusChange {
  StatusChange.fromJson(Map<String, dynamic> j)
      : status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        note = j['note'] as String,
        at = _dt(j['at']);
  final String status, statusLabel, note;
  final DateTime at;
}

class LetterSummary {
  LetterSummary.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        recipientName = j['recipientName'] as String,
        recipientType = j['recipientType'] as String,
        status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        preview = j['preview'] as String,
        updatedAt = _dt(j['updatedAt']),
        trackingCode = j['trackingCode'] as String?;
  final String id, recipientName, recipientType, status, statusLabel, preview;
  final DateTime updatedAt;
  final String? trackingCode;
}

class Photo {
  Photo.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        fileName = j['fileName'] as String,
        url = j['url'] as String;
  final String id, fileName, url;
}

/// Basılıp mektupla aynı zarfa konan belge (PDF / JPG / PNG).
class LetterDocument {
  LetterDocument.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        fileName = j['fileName'] as String,
        isPdf = j['isPdf'] as bool,
        url = j['url'] as String,
        pageCount = j['pageCount'] as int? ?? 1;
  final String id, fileName, url;
  final bool isPdf;

  /// Basılacak yaprak sayısı (PDF sayfa sayısı, görsel 1).
  final int pageCount;
}

class MyReview {
  MyReview.fromJson(Map<String, dynamic> j)
      : rating = j['rating'] as int,
        comment = j['comment'] as String,
        status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        rejectionReason = j['rejectionReason'] as String?;
  final int rating;
  final String comment, status, statusLabel;
  final String? rejectionReason;
}

class PendingPayment {
  PendingPayment.fromJson(Map<String, dynamic> j)
      : merchantOid = j['merchantOid'] as String,
        amount = _d(j['amount']),
        formUrl = j['formUrl'] as String?,
        simulation = j['simulation'] as bool,
        bankTransfer = j['bankTransfer'] == null
            ? null
            : BankTransferInstructions.fromJson(j['bankTransfer'] as Map<String, dynamic>);
  final String merchantOid;
  final double amount;
  final String? formUrl;
  final bool simulation;

  /// Havale / EFT bekleniyorsa: gönderilecek hesap, tutar ve açıklama.
  final BankTransferInstructions? bankTransfer;
}

/// Havale / EFT talimatı (sipariş oluşturulduktan sonra).
class BankTransferInstructions {
  BankTransferInstructions.fromJson(Map<String, dynamic> j)
      : bankName = j['bankName'] as String,
        accountHolder = j['accountHolder'] as String,
        iban = j['iban'] as String,
        amount = _d(j['amount']),
        reference = j['reference'] as String,
        note = j['note'] as String?,
        // Sunucu UTC saati bölge işareti olmadan gönderir.
        dueAt = DateTime.parse(_utc(j['dueAt'] as String)).toLocal(),
        discount = _d(j['discount']);
  final String bankName, accountHolder, iban, reference;
  final String? note;
  final double amount, discount;
  final DateTime dueAt;
}

/// Havale / EFT hesabı (ödeme adımında, sipariş oluşturulmadan önce).
class BankAccount {
  BankAccount.fromJson(Map<String, dynamic> j)
      : bankName = j['bankName'] as String,
        accountHolder = j['accountHolder'] as String,
        iban = j['iban'] as String,
        note = j['note'] as String?,
        validDays = j['validDays'] as int;
  final String bankName, accountHolder, iban;
  final String? note;
  final int validDays;
}

class Letter {
  Letter.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        senderName = j['senderName'] as String,
        senderAddress = Address.fromJson(j['senderAddress'] as Map<String, dynamic>),
        recipientName = j['recipientName'] as String,
        recipientType = j['recipientType'] as String,
        recipientDetail = j['recipientDetail'] as String?,
        recipientAddress = Address.fromJson(j['recipientAddress'] as Map<String, dynamic>),
        content = j['content'] as String,
        paper = j['paper'] as String,
        envelope = j['envelope'] as String,
        font = j['font'] as String,
        ink = j['ink'] as String,
        textSize = j['textSize'] as String? ?? 'Medium',
        scent = j['scent'] == null ? null : ScentInfo.fromJson(j['scent'] as Map<String, dynamic>),
        postcards = j['postcards'] == null ? const [] : _list(j['postcards'], PostcardInfo.fromJson),
        status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        pageCount = j['pageCount'] as int,
        paidAmount = _dn(j['paidAmount']),
        paymentMethod = j['paymentMethod'] as String?,
        trackingCode = j['trackingCode'] as String?,
        carrier = j['carrier'] as String?,
        carrierLastMovement = j['carrierLastMovement'] as String?,
        carrierLastMovementAt = _dtn(j['carrierLastMovementAt']),
        photos = _list(j['photos'], Photo.fromJson),
        history = _list(j['history'], StatusChange.fromJson),
        isEditable = j['isEditable'] as bool,
        canCancel = j['canCancel'] as bool,
        canDelete = j['canDelete'] as bool,
        canChangeAddress = j['canChangeAddress'] as bool,
        pointsUsed = _d(j['pointsUsed']),
        pointsDiscount = _d(j['pointsDiscount']),
        pointsToEarn = _d(j['pointsToEarn']),
        campaignTitle = j['campaignTitle'] as String?,
        campaignDiscount = _d(j['campaignDiscount']),
        createdAt = _dt(j['createdAt']),
        canReview = j['canReview'] as bool,
        review = j['review'] == null ? null : MyReview.fromJson(j['review'] as Map<String, dynamic>),
        pendingPayment =
            j['pendingPayment'] == null ? null : PendingPayment.fromJson(j['pendingPayment'] as Map<String, dynamic>),
        recipientFatherName = j['recipientFatherName'] as String?,
        recipientWard = j['recipientWard'] as String?,
        recipientNote = j['recipientNote'] as String?,
        documents = j['documents'] == null ? const [] : _list(j['documents'], LetterDocument.fromJson);

  /// Basılıp zarfa konan belgeler.
  final List<LetterDocument> documents;

  /// Cezaevi alıcısının baba adı / koğuşu; [recipientNote] zarfta yazan hali ("Baba adı: Ali · Koğuş: B-12").
  final String? recipientFatherName, recipientWard, recipientNote;

  final String id, senderName, recipientName, recipientType, content, paper, envelope, font, ink, status, statusLabel;

  /// Yazı boyutu: Small / Medium / Large.
  final String textSize;

  /// Seçilen koku (yoksa kokusuz).
  final ScentInfo? scent;
  final Address senderAddress, recipientAddress;
  final String? recipientDetail, paymentMethod, trackingCode, carrier, carrierLastMovement, campaignTitle;

  /// Seçilen kartpostallar (yoksa boş).
  final List<PostcardInfo> postcards;
  final int pageCount;
  final double? paidAmount;
  final DateTime? carrierLastMovementAt;
  final List<Photo> photos;
  final List<StatusChange> history;
  final bool isEditable, canCancel, canDelete, canChangeAddress, canReview;
  final double pointsUsed, pointsDiscount, pointsToEarn, campaignDiscount;
  final DateTime createdAt;
  final MyReview? review;
  final PendingPayment? pendingPayment;

  bool get isAwaitingPayment => status == 'AwaitingPayment';
}

/// Mektup kaydetme isteği (taslak oluştur / güncelle).
class LetterDraft {
  LetterDraft({
    this.senderName = '',
    this.senderAddress = const Address(),
    this.recipientName = '',
    this.recipientType = 'Inmate', // yeni mektupta varsayılan alıcı: cezaevi (sitedeki formla aynı)
    this.recipientDetail,
    this.recipientAddress = const Address(),
    this.content = '',
    this.paper = 'White',
    this.envelope = 'White',
    this.font = 'Serif',
    this.ink = 'Black',
    this.textSize = 'Medium',
    this.scentId,
    List<String>? postcardIds,
    this.recipientFatherName,
    this.recipientWard,
  }) : postcardIds = postcardIds ?? [];

  /// Aynı türdeki son mektubun alıcı ve gönderen bilgileri (/api/mobile/letters/prefill).
  factory LetterDraft.fromPrefill(Map<String, dynamic> j) => LetterDraft(
        recipientType: j['recipientType'] as String,
        recipientName: j['recipientName'] as String,
        recipientDetail: j['recipientDetail'] as String?,
        recipientFatherName: j['recipientFatherName'] as String?,
        recipientWard: j['recipientWard'] as String?,
        recipientAddress: Address.fromJson(j['recipientAddress'] as Map<String, dynamic>),
        senderName: j['senderName'] as String,
        senderAddress: Address.fromJson(j['senderAddress'] as Map<String, dynamic>),
      );

  factory LetterDraft.fromLetter(Letter l) => LetterDraft(
        senderName: l.senderName,
        senderAddress: l.senderAddress,
        recipientName: l.recipientName,
        recipientType: l.recipientType,
        recipientDetail: l.recipientDetail,
        recipientAddress: l.recipientAddress,
        content: l.content,
        paper: l.paper,
        envelope: l.envelope,
        font: l.font,
        ink: l.ink,
        textSize: l.textSize,
        scentId: l.scent?.id,
        postcardIds: [for (final p in l.postcards) if (p.id != null) p.id!],
        recipientFatherName: l.recipientFatherName,
        recipientWard: l.recipientWard,
      );

  String senderName, recipientName, recipientType, content, paper, envelope, font, ink, textSize;

  /// Seçilen koku (null: kokusuz).
  String? scentId;
  Address senderAddress, recipientAddress;
  String? recipientDetail, recipientFatherName, recipientWard;

  /// Katalogdan seçilen kartpostallar (seçim sırasıyla; boş: kartpostal istemiyorum).
  List<String> postcardIds;

  Map<String, dynamic> toJson() => {
        'senderName': senderName,
        'senderAddress': senderAddress.toJson(),
        'recipientName': recipientName,
        'recipientType': recipientType,
        'recipientDetail': recipientType == 'Standard' ? null : recipientDetail,
        'recipientAddress': recipientAddress.toJson(),
        'content': content,
        'paper': paper,
        'envelope': envelope,
        'font': font,
        'ink': ink,
        'size': textSize,
        'scentId': scentId,
        'postcardIds': postcardIds,
        'recipientFatherName': recipientType == 'Inmate' ? recipientFatherName : null,
        'recipientWard': recipientType == 'Inmate' ? recipientWard : null,
      };
}

class QuoteLine {
  QuoteLine(this.description, this.amount);
  final String description;
  final double amount;
}

class Quote {
  Quote.fromJson(Map<String, dynamic> j)
      : method = j['method'] as String,
        lines = (j['lines'] as List)
            .map((e) => QuoteLine(e['description'] as String, _d(e['amount'])))
            .toList(),
        total = _d(j['total']),
        campaignDiscount = _d(j['campaignDiscount']),
        campaignTitle = j['campaignTitle'] as String?,
        usePoints = j['usePoints'] as bool,
        pointsBalance = _d(j['pointsBalance']),
        pointsUsed = _d(j['pointsUsed']),
        pointsDiscount = _d(j['pointsDiscount']),
        balanceValue = _d(j['balanceValue']),
        amountToPay = _d(j['amountToPay']),
        pointsToEarn = _d(j['pointsToEarn']),
        loyaltyEnabled = j['loyaltyEnabled'] as bool,
        mobilePaymentAvailable = j['mobilePaymentAvailable'] as bool,
        providerLabel = j['providerLabel'] as String,
        providerIsSimulation = j['providerIsSimulation'] as bool,
        couponCode = j['couponCode'] as String?,
        couponApplied = j['couponApplied'] as bool,
        couponMessage = j['couponMessage'] as String?,
        bankTransferAvailable = j['bankTransferAvailable'] as bool? ?? false,
        bankTransferPercent = _d(j['bankTransferPercent'] ?? 0),
        bankTransferDiscount = _d(j['bankTransferDiscount'] ?? 0),
        bankAccount = j['bankAccount'] == null ? null : BankAccount.fromJson(j['bankAccount'] as Map<String, dynamic>);

  final String method, providerLabel;
  final List<QuoteLine> lines;
  final double total, campaignDiscount, pointsBalance, pointsUsed, pointsDiscount, balanceValue, amountToPay, pointsToEarn;
  final String? campaignTitle, couponCode, couponMessage;
  final bool usePoints, loyaltyEnabled, mobilePaymentAvailable, providerIsSimulation, couponApplied;
  final bool bankTransferAvailable;
  final double bankTransferPercent, bankTransferDiscount;
  final BankAccount? bankAccount;

  double get subtotal => lines.fold(0, (s, l) => s + l.amount);
}

class PayResult {
  PayResult.fromJson(Map<String, dynamic> j)
      : completed = j['completed'] as bool,
        merchantOid = j['merchantOid'] as String,
        formUrl = j['formUrl'] as String?,
        simulation = j['simulation'] as bool,
        returnUrl = j['returnUrl'] as String;
  final bool completed, simulation;
  final String merchantOid, returnUrl;
  final String? formUrl;
}

class PaymentState {
  PaymentState.fromJson(Map<String, dynamic> j)
      : merchantOid = j['merchantOid'] as String,
        status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        amount = _d(j['amount']),
        failureReason = j['failureReason'] as String?;
  final String merchantOid, status, statusLabel;
  final double amount;
  final String? failureReason;

  bool get isPending => status == 'Pending';
  bool get isSucceeded => status == 'Succeeded';
}

class PointTransaction {
  PointTransaction.fromJson(Map<String, dynamic> j)
      : type = j['type'] as String,
        typeLabel = j['typeLabel'] as String,
        points = _d(j['points']),
        letterId = j['letterId'] as String?,
        note = j['note'] as String,
        at = _dt(j['at']);
  final String type, typeLabel, note;
  final double points;
  final String? letterId;
  final DateTime at;
}

class PointsInfo {
  PointsInfo.fromJson(Map<String, dynamic> j)
      : balance = _d(j['balance']),
        balanceValue = _d(j['balanceValue']),
        totalEarned = _d(j['totalEarned']),
        pointValue = _d(j['pointValue']),
        isEnabled = j['isEnabled'] as bool,
        earnRule = j['earnRule'] as String?,
        referralEnabled = j['referralEnabled'] as bool,
        referralRule = j['referralRule'] as String?,
        referralCode = j['referralCode'] as String,
        referredCount = j['referredCount'] as int,
        transactions = _list(j['transactions'], PointTransaction.fromJson);
  final double balance, balanceValue, totalEarned, pointValue;
  final bool isEnabled, referralEnabled;
  final String? earnRule, referralRule;
  final String referralCode;
  final int referredCount;
  final List<PointTransaction> transactions;
}

class CampaignCard {
  CampaignCard.fromJson(Map<String, dynamic> j)
      : id = j['id'] as String,
        title = j['title'] as String,
        slug = j['slug'] as String,
        summary = j['summary'] as String,
        badgeText = j['badgeText'] as String?,
        couponCode = j['couponCode'] as String?,
        phase = j['phase'] as String,
        statusText = j['statusText'] as String,
        dateRange = j['dateRange'] as String,
        audienceText = j['audienceText'] as String?,
        coverUrl = j['coverUrl'] as String?,
        description = j['description'] as String?,
        minOrderAmount = _dn(j['minOrderAmount']);
  final String id, title, slug, summary, phase, statusText, dateRange;
  final String? badgeText, couponCode, audienceText, coverUrl, description;
  final double? minOrderAmount;

  bool get isActive => phase == 'Active';
}

class Review {
  Review.fromJson(Map<String, dynamic> j)
      : authorName = j['authorName'] as String,
        recipientCity = j['recipientCity'] as String?,
        rating = j['rating'] as int,
        comment = j['comment'] as String,
        createdAt = _dt(j['createdAt']);
  final String authorName, comment;
  final String? recipientCity;
  final int rating;
  final DateTime createdAt;
}

class ReviewPage {
  ReviewPage.fromJson(Map<String, dynamic> j)
      : average = _d(j['average']),
        count = j['count'] as int,
        items = _list(j['items'], Review.fromJson),
        page = j['page'] as int,
        hasNext = j['hasNext'] as bool;
  final double average;
  final int count, page;
  final List<Review> items;
  final bool hasNext;
}

class Tracking {
  Tracking.fromJson(Map<String, dynamic> j)
      : trackingCode = j['trackingCode'] as String,
        recipientInitials = j['recipientInitials'] as String,
        city = j['city'] as String,
        status = j['status'] as String,
        statusLabel = j['statusLabel'] as String,
        history = _list(j['history'], StatusChange.fromJson);
  final String trackingCode, recipientInitials, city, status, statusLabel;
  final List<StatusChange> history;
}

/// Onay bekleyen e-posta / telefon değişikliği ([kind]: Email / Phone).
class PendingContactChange {
  PendingContactChange.fromJson(Map<String, dynamic> j)
      : kind = j['kind'] as String,
        destination = j['destination'] as String,
        expiresAt = DateTime.parse(_utc(j['expiresAt'] as String)).toLocal(),
        remainingAttempts = j['remainingAttempts'] as int;
  final String kind, destination;
  final DateTime expiresAt;
  final int remainingAttempts;
}
