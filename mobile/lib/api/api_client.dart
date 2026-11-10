import 'dart:async';
import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import '../config.dart';

/// Sunucunun döndürdüğü, kullanıcıya gösterilebilir hata.
class ApiException implements Exception {
  ApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;

  bool get isUnauthorized => statusCode == 401;

  @override
  String toString() => message;
}

/// /api/mobile uç noktalarını çağırır. Erişim token'ı süresi dolunca yenileme token'ıyla
/// bir kez yenilenir; yenilenemezse [onSessionExpired] çağrılır (giriş ekranına dönülür).
class ApiClient {
  ApiClient({http.Client? httpClient, FlutterSecureStorage? storage})
      : _http = httpClient ?? http.Client(),
        _storage = storage ?? const FlutterSecureStorage();

  final http.Client _http;
  final FlutterSecureStorage _storage;

  static const _accessKey = 'access_token';
  static const _refreshKey = 'refresh_token';
  static const _timeout = Duration(seconds: 25);

  String? _accessToken;
  String? _refreshToken;
  Future<bool>? _refreshing;

  void Function()? onSessionExpired;

  String get baseUrl => AppConfig.apiBaseUrl;
  bool get hasSession => _refreshToken != null;
  Map<String, String> get authHeaders =>
      _accessToken == null ? const {} : {'Authorization': 'Bearer $_accessToken'};

  /// Sunucunun verdiği göreli adresi (ör. fotoğraf) tam adrese çevirir.
  String absolute(String path) => path.startsWith('http') ? path : '$baseUrl$path';

  Future<void> restoreSession() async {
    try {
      _accessToken = await _storage.read(key: _accessKey);
      _refreshToken = await _storage.read(key: _refreshKey);
    } catch (_) {
      // Anahtar deposu okunamazsa (ör. uygulama yeniden yüklendi) yeniden giriş istenir.
      _accessToken = _refreshToken = null;
    }
  }

  Future<void> saveTokens(Map<String, dynamic> json) async {
    _accessToken = json['accessToken'] as String?;
    _refreshToken = json['refreshToken'] as String?;
    await _storage.write(key: _accessKey, value: _accessToken);
    await _storage.write(key: _refreshKey, value: _refreshToken);
  }

  Future<void> clearTokens() async {
    _accessToken = _refreshToken = null;
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }

  // ---------------- HTTP ----------------

  Future<dynamic> get(String path, {Map<String, String?>? query}) => _send('GET', path, query: query);
  Future<dynamic> post(String path, [Object? body]) => _send('POST', path, body: body);
  Future<dynamic> put(String path, [Object? body]) => _send('PUT', path, body: body);
  Future<dynamic> delete(String path) => _send('DELETE', path);

  /// Fotoğraf yükleme (multipart/form-data, alan adı "photo").
  Future<dynamic> upload(String path, List<int> bytes, String fileName) {
    return _withAuth(() async {
      final req = http.MultipartRequest('POST', Uri.parse('$baseUrl$path'))
        ..headers.addAll(authHeaders)
        ..files.add(http.MultipartFile.fromBytes('photo', bytes, filename: fileName));
      final streamed = await _http.send(req).timeout(const Duration(seconds: 90));
      return http.Response.fromStream(streamed);
    });
  }

  Future<dynamic> _send(String method, String path, {Object? body, Map<String, String?>? query}) {
    final params = <String, String>{
      for (final e in (query ?? const <String, String?>{}).entries)
        if (e.value != null && e.value!.isNotEmpty) e.key: e.value!,
    };
    final uri = Uri.parse('$baseUrl$path').replace(queryParameters: params.isEmpty ? null : params);
    return _withAuth(() {
      final headers = {
        'Accept': 'application/json',
        if (body != null) 'Content-Type': 'application/json; charset=utf-8',
        ...authHeaders,
      };
      final encoded = body == null ? null : jsonEncode(body);
      final Future<http.Response> f = switch (method) {
        'GET' => _http.get(uri, headers: headers),
        'POST' => _http.post(uri, headers: headers, body: encoded),
        'PUT' => _http.put(uri, headers: headers, body: encoded),
        _ => _http.delete(uri, headers: headers),
      };
      return f.timeout(_timeout);
    });
  }

  Future<dynamic> _withAuth(Future<http.Response> Function() call) async {
    http.Response res;
    try {
      res = await call();
      if (res.statusCode == 401 && _refreshToken != null && await _refresh()) {
        res = await call();
      }
    } on TimeoutException {
      throw ApiException('Sunucu yanıt vermedi. İnternet bağlantınızı kontrol edip tekrar deneyin.');
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException('Sunucuya bağlanılamadı. İnternet bağlantınızı kontrol edin.');
    }
    if (res.statusCode == 401 && _refreshToken != null) {
      await clearTokens();
      onSessionExpired?.call();
    }
    return _decode(res);
  }

  /// Aynı anda gelen 401'ler için tek yenileme isteği yapılır.
  Future<bool> _refresh() {
    return _refreshing ??= () async {
      try {
        final res = await _http
            .post(Uri.parse('$baseUrl/api/mobile/auth/refresh'),
                headers: {'Content-Type': 'application/json'}, body: jsonEncode({'refreshToken': _refreshToken}))
            .timeout(_timeout);
        if (res.statusCode != 200) return false;
        await saveTokens(jsonDecode(utf8.decode(res.bodyBytes)) as Map<String, dynamic>);
        return true;
      } catch (_) {
        return false;
      }
    }()
        .whenComplete(() => _refreshing = null);
  }

  static dynamic _decode(http.Response res) {
    final text = utf8.decode(res.bodyBytes);
    dynamic json;
    if (text.isNotEmpty) {
      try {
        json = jsonDecode(text);
      } catch (_) {
        json = null;
      }
    }
    if (res.statusCode >= 200 && res.statusCode < 300) return json;
    throw ApiException(_errorMessage(json, res.statusCode), statusCode: res.statusCode);
  }

  static String _errorMessage(dynamic json, int status) {
    if (json is Map<String, dynamic>) {
      if (json['message'] is String) return json['message'] as String;
      // ASP.NET doğrulama hatası: { errors: { alan: [mesaj] } }
      final errors = json['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        if (first is List && first.isNotEmpty) return first.first.toString();
      }
    }
    return switch (status) {
      401 => 'Oturumunuz sona erdi. Lütfen tekrar giriş yapın.',
      404 => 'Aradığınız kayıt bulunamadı.',
      >= 500 => 'Sunucuda bir hata oluştu. Lütfen biraz sonra tekrar deneyin.',
      _ => 'İşlem tamamlanamadı (hata $status).',
    };
  }
}
