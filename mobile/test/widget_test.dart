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
        'acquisitionChannels'
      ])
        k: [
          {'value': 'White', 'label': 'Beyaz'}
        ],
      'scents': [
        {'id': 's1', 'name': 'Manolya'},
        {'id': 's2', 'name': 'Lavanta'}
      ],
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
    test('kokular yönetim panelindeki listeden gelir', () {
      expect(s.scents.map((x) => x.name), ['Manolya', 'Lavanta']);
      expect(s.scents.first.id, 's1');
    });
    test('eski sunucu belge sınırı göndermezse varsayılanlar kullanılır', () {
      expect(s.maxDocuments, 5);
      expect(s.maxDocumentBytes, 10 * 1024 * 1024);
    });
  });

  group('Locations (il / ilçe)', () {
    final loc = Locations.fromJson([
      {'name': 'Ankara', 'districts': ['Çankaya', 'Keçiören']},
      {'name': 'Iğdır', 'districts': ['Merkez']},
      {'name': 'İstanbul', 'districts': ['Kadıköy', 'Üsküdar']},
    ]);

    test('Türkçe karakter ve büyük/küçük harf farkı yok sayılır', () {
      expect(loc.find('Istanbul')?.name, 'İstanbul');
      expect(loc.find(' ISTANBUL ')?.name, 'İstanbul');
      expect(loc.find('igdir')?.name, 'Iğdır');
      expect(loc.find('Atlantis'), isNull);
    });
    test('ilçe yazımı listedekine çevrilir', () {
      expect(loc.findDistrict(loc.find('İstanbul'), 'Kadikoy'), 'Kadıköy');
      expect(loc.findDistrict(loc.find('Ankara'), 'cankaya'), 'Çankaya');
      expect(loc.findDistrict(loc.find('Ankara'), 'Kadıköy'), isNull);
    });
  });

  group('LetterDraft.toJson', () {
    test('standart alıcıda ek bilgi gönderilmez; kartpostal seçilmediyse null', () {
      final j = LetterDraft(recipientType: 'Standard', recipientDetail: 'x').toJson();
      expect(j['recipientDetail'], isNull);
      expect(j['postcardIds'], isEmpty);
    });
    test('yeni mektupta varsayılan alıcı türü cezaevi', () => expect(LetterDraft().recipientType, 'Inmate'));
    test('baba adı ve koğuş yalnızca cezaevi alıcısında gönderilir', () {
      final inmate = LetterDraft(recipientType: 'Inmate', recipientFatherName: 'Ali', recipientWard: 'B-12').toJson();
      expect(inmate['recipientFatherName'], 'Ali');
      expect(inmate['recipientWard'], 'B-12');
      final soldier = LetterDraft(recipientType: 'Soldier', recipientFatherName: 'Ali', recipientWard: 'B-12').toJson();
      expect(soldier['recipientFatherName'], isNull);
      expect(soldier['recipientWard'], isNull);
    });
    test('seçilen kartpostalların kimlikleri sırasıyla gönderilir', () {
      final j = LetterDraft(postcardIds: ['a', 'b']).toJson();
      expect(j['postcardIds'], ['a', 'b']);
      expect(j.containsKey('postcardId'), isFalse);
    });
    test('her taslağın kendi kartpostal listesi vardır', () {
      final a = LetterDraft()..postcardIds.add('x');
      expect(LetterDraft().postcardIds, isEmpty);
      expect(a.postcardIds, ['x']);
    });
  });

  group('PostcardInfo', () {
    test('katalog kaydı ve eski (görselsiz) sipariş okunur', () {
      final p = PostcardInfo.fromJson({'id': 'x', 'title': 'Gül Bahçesi', 'category': 'Aşk', 'imageUrl': '/kartpostal/x/gorsel'});
      expect(p.title, 'Gül Bahçesi');
      expect(p.imageUrl, '/kartpostal/x/gorsel');
      final legacy = PostcardInfo.fromJson({'id': null, 'title': 'Aşk #2', 'category': 'Aşk', 'imageUrl': null});
      expect(legacy.id, isNull);
      expect(legacy.imageUrl, isNull);
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
