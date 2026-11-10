import 'package:flutter/widgets.dart';

import '../api/api_client.dart';
import '../api/bimektup_api.dart';
import '../api/models.dart';

enum SessionStatus { loading, signedOut, signedIn, offline }

/// Uygulamanın ortak durumu: oturum, sunucu ayarları (fiyat, seçenekler) ve üye bilgileri.
class Session extends ChangeNotifier {
  Session({ApiClient? client}) : api = BiMektupApi(client ?? ApiClient()) {
    api.client.onSessionExpired = () {
      me = null;
      _set(SessionStatus.signedOut, message: 'Oturumunuz sona erdi. Lütfen tekrar giriş yapın.');
    };
  }

  final BiMektupApi api;

  SessionStatus status = SessionStatus.loading;
  AppSettings? settings;
  Me? me;
  String? message;

  /// Mektup listesi gibi ekranların yenilenmesi için sayaç (ödeme / kayıt sonrası artar).
  int lettersVersion = 0;

  Future<void> start() async {
    _set(SessionStatus.loading);
    try {
      settings = await api.config();
      await api.client.restoreSession();
      if (api.client.hasSession) {
        me = await api.me();
        _set(SessionStatus.signedIn);
      } else {
        _set(SessionStatus.signedOut);
      }
    } on ApiException catch (e) {
      if (e.isUnauthorized) {
        _set(SessionStatus.signedOut);
      } else {
        _set(SessionStatus.offline, message: e.message);
      }
    }
  }

  Future<void> login(String email, String password) async {
    await api.login(email, password);
    me = await api.me();
    _set(SessionStatus.signedIn);
  }

  Future<void> afterRegister() async {
    me = await api.me();
    _set(SessionStatus.signedIn);
  }

  Future<void> refreshMe() async {
    me = await api.me();
    notifyListeners();
  }

  void lettersChanged() {
    lettersVersion++;
    notifyListeners();
  }

  Future<void> logout() async {
    await api.client.clearTokens();
    me = null;
    _set(SessionStatus.signedOut);
  }

  void _set(SessionStatus s, {String? message}) {
    status = s;
    this.message = message;
    notifyListeners();
  }
}

/// Session'a ağaçtan erişim: `AppScope.of(context)` (dinleyerek) / `AppScope.read(context)`.
class AppScope extends InheritedNotifier<Session> {
  const AppScope({super.key, required Session session, required super.child}) : super(notifier: session);

  static Session of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AppScope>()!.notifier!;

  static Session read(BuildContext context) =>
      (context.getElementForInheritedWidgetOfExactType<AppScope>()!.widget as AppScope).notifier!;
}
