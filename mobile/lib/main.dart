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

class BiMektupApp extends StatelessWidget {
  const BiMektupApp({super.key, required this.session});
  final Session session;

  @override
  Widget build(BuildContext context) {
    return AppScope(
      session: session,
      child: MaterialApp(
        title: 'BiMektup',
        debugShowCheckedModeBanner: false,
        theme: buildTheme(),
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
          Image.asset('assets/images/logo.png', width: 180),
          const SizedBox(height: 32),
          const CircularProgressIndicator(),
        ]),
      ),
    );
  }
}
