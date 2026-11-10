import 'package:flutter/foundation.dart';

/// Sunucu adresi. Derlerken verilebilir:
///   flutter run --dart-define=API_BASE_URL=https://bimektup.net
/// Verilmezse geliştirme sunucusu kullanılır (Android emülatöründe bilgisayarın localhost'u 10.0.2.2'dir).
class AppConfig {
  static const _fromBuild = String.fromEnvironment('API_BASE_URL');

  static String get apiBaseUrl {
    if (_fromBuild.isNotEmpty) return _fromBuild.replaceAll(RegExp(r'/+$'), '');
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) return 'http://10.0.2.2:5213';
    return 'http://localhost:5213';
  }
}
