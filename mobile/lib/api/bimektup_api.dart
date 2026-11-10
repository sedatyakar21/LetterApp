import 'api_client.dart';
import 'models.dart';

/// /api/mobile uç noktalarının tipli karşılıkları.
class BiMektupApi {
  BiMektupApi(this.client);
  final ApiClient client;

  // ---------------- Hesap ----------------

  Future<void> login(String email, String password) async =>
      client.saveTokens(await client.post('/api/mobile/auth/login', {'email': email, 'password': password}));

  Future<void> register({
    required String fullName,
    required String email,
    required String phone,
    required String password,
    required String? channel,
    String? referralCode,
    DateTime? birthDate,
    required bool acceptTerms,
  }) async =>
      client.saveTokens(await client.post('/api/mobile/auth/register', {
        'fullName': fullName,
        'email': email,
        'phone': phone,
        'password': password,
        'channel': channel,
        'referralCode': referralCode,
        'birthDate': birthDate == null ? null : _date(birthDate),
        'acceptTerms': acceptTerms,
      }));

  Future<AppSettings> config() async => AppSettings.fromJson(await client.get('/api/mobile/config'));

  Future<Me> me() async => Me.fromJson(await client.get('/api/mobile/me'));

  Future<void> setBirthDate(DateTime date) => client.post('/api/mobile/me/birth-date', {'birthDate': _date(date)});

  Future<PointsInfo> points() async => PointsInfo.fromJson(await client.get('/api/mobile/me/points'));

  // ---------------- Mektuplar ----------------

  Future<List<LetterSummary>> letters() async =>
      (await client.get('/api/mobile/letters') as List).map((e) => LetterSummary.fromJson(e)).toList();

  Future<Letter> letter(String id) async => Letter.fromJson(await client.get('/api/mobile/letters/$id'));

  Future<Letter> saveLetter(String? id, LetterDraft draft) async => Letter.fromJson(id == null
      ? await client.post('/api/mobile/letters', draft.toJson())
      : await client.put('/api/mobile/letters/$id', draft.toJson()));

  Future<void> deleteLetter(String id) => client.delete('/api/mobile/letters/$id');

  Future<Letter> cancelLetter(String id) async => Letter.fromJson(await client.post('/api/mobile/letters/$id/cancel'));

  Future<Letter> changeAddress(String id, Address a) async =>
      Letter.fromJson(await client.put('/api/mobile/letters/$id/address', a.toJson()));

  Future<Letter> addPhoto(String id, List<int> bytes, String fileName) async =>
      Letter.fromJson(await client.upload('/api/mobile/letters/$id/photos', bytes, fileName));

  Future<Letter> removePhoto(String id, String photoId) async =>
      Letter.fromJson(await client.delete('/api/mobile/letters/$id/photos/$photoId'));

  Future<Letter> review(String id, int rating, String comment) async =>
      Letter.fromJson(await client.post('/api/mobile/letters/$id/review', {'rating': rating, 'comment': comment}));

  // ---------------- Ödeme ----------------

  Future<Quote> quote(String id, {required String method, required bool usePoints, String? coupon}) async =>
      Quote.fromJson(await client.get('/api/mobile/letters/$id/quote',
          query: {'method': method, 'usePoints': '$usePoints', 'coupon': coupon}));

  Future<PayResult> pay(String id, {required String method, required bool usePoints, String? coupon}) async =>
      PayResult.fromJson(await client
          .post('/api/mobile/letters/$id/pay', {'method': method, 'usePoints': usePoints, 'coupon': coupon}));

  Future<Letter> abandonPayment(String id) async =>
      Letter.fromJson(await client.post('/api/mobile/letters/$id/pay/abandon'));

  Future<PaymentState> paymentStatus(String oid) async =>
      PaymentState.fromJson(await client.get('/api/mobile/letters/payments/${Uri.encodeComponent(oid)}'));

  Future<PaymentState> simulatePayment(String oid, bool success) async => PaymentState.fromJson(
      await client.post('/api/mobile/letters/payments/${Uri.encodeComponent(oid)}/simulate', {'success': success}));

  // ---------------- Herkese açık ----------------

  Future<List<CampaignCard>> campaigns() async =>
      (await client.get('/api/mobile/campaigns') as List).map((e) => CampaignCard.fromJson(e)).toList();

  Future<CampaignCard> campaign(String slug) async =>
      CampaignCard.fromJson(await client.get('/api/mobile/campaigns/${Uri.encodeComponent(slug)}'));

  Future<ReviewPage> reviews({int page = 1}) async =>
      ReviewPage.fromJson(await client.get('/api/mobile/reviews', query: {'page': '$page'}));

  Future<Tracking> track(String code) async =>
      Tracking.fromJson(await client.get('/api/mobile/tracking/${Uri.encodeComponent(code.trim())}'));

  static String _date(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
