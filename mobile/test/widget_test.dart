import 'dart:convert';

import 'package:bimektup/api/api_client.dart';
import 'package:bimektup/api/models.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Map<String, dynamic> _config() => {
      'prices': {
        'basePrice': 290,
        'extraPage': 30,
        'photo': 20,
        'postcard': 35,
        'coloredPaper': 15,
        'coloredEnvelope': 15,
        'scent': 40,
        'mobilePaymentSurchargeRate': 0.3,
      },
      for (final k in [
        'recipientTypes',
        'paperColors',
        'envelopeColors',
        'fonts',
        'inks',
        'scents',
        'postcardCategories',
        'acquisitionChannels'
      ])
        k: [
          {'value': 'White', 'label': 'Beyaz'}
        ],
      'postcardDesignsPerCategory': 4,
      'maxPhotos': 10,
      'maxPhotoBytes': 5242880,
      'maxContentLength': 25000,
      'charsPerPage': 2200,
      'charsPerLineBreak': 60,
      'minPasswordLength': 8,
      'loyaltyEnabled': true,
      'mobilePaymentAvailable': true,
      'paymentSimulation': true,
      'support': {'whatsAppNumber': '905434277791', 'whatsAppDisplay': '0543 427 77 91', 'email': null, 'hours': null},
    };

void main() {
  group('AppSettings.pageCount (sunucudaki LetterPages kuralıyla aynı)', () {
    final s = AppSettings.fromJson(_config());

    test('boş metin 1 sayfa', () => expect(s.pageCount(''), 1));
    test('2200 karakter 1 sayfa, 2201 karakter 2 sayfa', () {
      expect(s.pageCount('a' * 2200), 1);
      expect(s.pageCount('a' * 2201), 2);
    });
    test('her satır sonu 60 karakter sayılır', () {
      // 2100 karakter + 2 satır sonu (2×60) = 2222 → 2 sayfa
      expect(s.pageCount('${'a' * 1050}\n\n${'a' * 1048}'), 2);
    });
    test('etiket bulunamazsa değerin kendisi döner', () => expect(s.label(s.paperColors, 'Pink'), 'Pink'));
  });

  group('LetterDraft.toJson', () {
    test('standart alıcıda ek bilgi gönderilmez; kartpostal yoksa tasarım null', () {
      final j = LetterDraft(recipientType: 'Standard', recipientDetail: 'x', postcardDesign: 3).toJson();
      expect(j['recipientDetail'], isNull);
      expect(j['postcardDesign'], isNull);
    });
    test('kartpostal seçiliyse tasarım varsayılan 1', () {
      final j = LetterDraft(postcardCategory: 'Love').toJson();
      expect(j['postcardDesign'], 1);
    });
  });

  group('ApiClient hata mesajları', () {
    ApiClient client(int status, Object? body) => ApiClient(
        httpClient: MockClient((_) async => http.Response.bytes(
            utf8.encode(body == null ? '' : jsonEncode(body)), status,
            headers: {'content-type': 'application/json; charset=utf-8'})));

    test('sunucunun Türkçe mesajı gösterilir', () async {
      final c = client(400, {'message': 'E-posta veya şifre hatalı.'});
      expect(() => c.get('/x'), throwsA(isA<ApiException>().having((e) => e.message, 'message', 'E-posta veya şifre hatalı.')));
    });
    test('doğrulama hatasında ilk alan mesajı', () async {
      final c = client(400, {
        'errors': {
          'Email': ['Geçerli bir e-posta girin.']
        }
      });
      expect(() => c.get('/x'), throwsA(isA<ApiException>().having((e) => e.message, 'message', 'Geçerli bir e-posta girin.')));
    });
    test('500 hatasında genel mesaj', () async {
      final c = client(500, null);
      expect(() => c.get('/x'), throwsA(isA<ApiException>().having((e) => e.statusCode, 'statusCode', 500)));
    });
    test('başarılı yanıt JSON olarak çözülür (Türkçe karakterler korunur)', () async {
      final c = client(200, {'name': 'Ayşe Işık'});
      expect((await c.get('/x'))['name'], 'Ayşe Işık');
    });
  });
}
