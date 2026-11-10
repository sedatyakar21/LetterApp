// Sunucudan gelen JSON modelleri (alan adları /api/mobile yanıtlarıyla aynıdır).

double _d(dynamic v) => v == null ? 0 : (v as num).toDouble();
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

/// Formdaki seçenekler, fiyatlar ve sınırlar (/api/mobile/config).
class AppSettings {
  AppSettings.fromJson(Map<String, dynamic> j)
      : prices = Prices(j['prices'] as Map<String, dynamic>),
        recipientTypes = _list(j['recipientTypes'], Option.fromJson),
        paperColors = _list(j['paperColors'], Option.fromJson),
        envelopeColors = _list(j['envelopeColors'], Option.fromJson),
        fonts = _list(j['fonts'], Option.fromJson),
        inks = _list(j['inks'], Option.fromJson),
        scents = _list(j['scents'], Option.fromJson),
        postcardCategories = _list(j['postcardCategories'], Option.fromJson),
        acquisitionChannels = _list(j['acquisitionChannels'], Option.fromJson),
        postcardDesigns = j['postcardDesignsPerCategory'] as int,
        maxPhotos = j['maxPhotos'] as int,
        maxPhotoBytes = j['maxPhotoBytes'] as int,
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
  final List<Option> recipientTypes, paperColors, envelopeColors, fonts, inks, scents, postcardCategories, acquisitionChannels;
  final int postcardDesigns, maxPhotos, maxPhotoBytes, maxContentLength, charsPerPage, charsPerLineBreak, minPasswordLength;
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
        loyaltyEnabled = j['loyaltyEnabled'] as bool;

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
  String toString() => '$line, $district / $city $postalCode';
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
        simulation = j['simulation'] as bool;
  final String merchantOid;
  final double amount;
  final String? formUrl;
  final bool simulation;
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
        scent = j['scent'] as String,
        postcardCategory = j['postcardCategory'] as String?,
        postcardDesign = j['postcardDesign'] as int?,
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
            j['pendingPayment'] == null ? null : PendingPayment.fromJson(j['pendingPayment'] as Map<String, dynamic>);

  final String id, senderName, recipientName, recipientType, content, paper, envelope, font, ink, scent, status, statusLabel;
  final Address senderAddress, recipientAddress;
  final String? recipientDetail, postcardCategory, paymentMethod, trackingCode, carrier, carrierLastMovement, campaignTitle;
  final int? postcardDesign;
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
    this.recipientType = 'Standard',
    this.recipientDetail,
    this.recipientAddress = const Address(),
    this.content = '',
    this.paper = 'White',
    this.envelope = 'White',
    this.font = 'Serif',
    this.ink = 'Black',
    this.scent = 'None',
    this.postcardCategory,
    this.postcardDesign,
  });

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
        scent: l.scent,
        postcardCategory: l.postcardCategory,
        postcardDesign: l.postcardDesign,
      );

  String senderName, recipientName, recipientType, content, paper, envelope, font, ink, scent;
  Address senderAddress, recipientAddress;
  String? recipientDetail, postcardCategory;
  int? postcardDesign;

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
        'scent': scent,
        'postcardCategory': postcardCategory,
        'postcardDesign': postcardCategory == null ? null : (postcardDesign ?? 1),
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
        couponMessage = j['couponMessage'] as String?;

  final String method, providerLabel;
  final List<QuoteLine> lines;
  final double total, campaignDiscount, pointsBalance, pointsUsed, pointsDiscount, balanceValue, amountToPay, pointsToEarn;
  final String? campaignTitle, couponCode, couponMessage;
  final bool usePoints, loyaltyEnabled, mobilePaymentAvailable, providerIsSimulation, couponApplied;

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
