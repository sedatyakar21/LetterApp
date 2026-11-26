import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

import 'screens/auth/login_screen.dart';
import 'screens/home_shell.dart';
import 'state/session.dart';
import 'ui/common.dart';
import 'ui/theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  Intl.defaultLocale = 'tr_TR';
  await initializeDateFormatting('tr_TR');
  final session = Session();
  runApp(BiMektupApp(session: session));
  session.start();
}

class BiMektupApp extends StatefulWidget {
  const BiMektupApp({super.key, required this.session});
  final Session session;

  @override
  State<BiMektupApp> createState() => _BiMektupAppState();
}

/// Tema: kullanıcının seçimi (Sistem / Açık / Koyu) ve telefonun açık-koyu ayarı izlenir.
/// Renkler [Brand] üzerinden okunduğu için tema değişince tüm ekranlar yeniden çizilir.
class _BiMektupAppState extends State<BiMektupApp> with WidgetsBindingObserver {
  Brightness? _applied;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    widget.session.addListener(_onSession);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    widget.session.removeListener(_onSession);
    super.dispose();
  }

  @override
  void didChangePlatformBrightness() => setState(() {});

  void _onSession() {
    if (_brightness != _applied) setState(() {});
  }

  Brightness get _brightness => switch (widget.session.themeMode) {
        ThemeMode.light => Brightness.light,
        ThemeMode.dark => Brightness.dark,
        ThemeMode.system => WidgetsBinding.instance.platformDispatcher.platformBrightness,
      };

  /// Sabit (const) olmayan tüm ekranların [Brand] renklerini yeniden okuması için.
  void _rebuildAll() {
    void mark(Element e) {
      e.markNeedsBuild();
      e.visitChildren(mark);
    }

    (context as Element).visitChildren(mark);
  }

  @override
  Widget build(BuildContext context) {
    final brightness = _brightness;
    final changed = _applied != null && _applied != brightness;
    _applied = brightness;
    final theme = buildTheme(brightness);
    if (changed) WidgetsBinding.instance.addPostFrameCallback((_) => _rebuildAll());
    final session = widget.session;
    return AppScope(
      session: session,
      child: MaterialApp(
        title: 'BiMektubum',
        debugShowCheckedModeBanner: false,
        theme: theme,
        themeAnimationDuration: Duration.zero,
        locale: const Locale('tr', 'TR'),
        supportedLocales: const [Locale('tr', 'TR')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: const RootGate(),
      ),
    );
  }
}

/// Oturum durumuna göre giriş ekranını veya ana ekranı gösterir.
class RootGate extends StatelessWidget {
  const RootGate({super.key});

  @override
  Widget build(BuildContext context) {
    final session = AppScope.of(context);
    return switch (session.status) {
      SessionStatus.loading => const _Splash(),
      SessionStatus.offline => Scaffold(
          body: SafeArea(child: ErrorView(message: session.message ?? 'Bağlantı kurulamadı.', onRetry: session.start)),
        ),
      SessionStatus.signedOut => const LoginScreen(),
      SessionStatus.signedIn => const HomeShell(),
    };
  }
}

class _Splash extends StatelessWidget {
  const _Splash();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const BrandLogo(width: 180),
          const SizedBox(height: 32),
          const CircularProgressIndicator(),
        ]),
      ),
    );
  }
}
