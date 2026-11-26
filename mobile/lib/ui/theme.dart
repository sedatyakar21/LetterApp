import 'package:flutter/material.dart';

/// Sitedeki marka renkleri (site.css). Zemine bağlı renkler (yazı, çizgi, kağıt, vurgu) koyu modda
/// okunaklı karşılıklarına döner; [dark] uygulama kökünde temayla birlikte güncellenir.
class Brand {
  /// Etkin tema koyu mu (kökteki tema seçiminden ayarlanır).
  static bool dark = false;

  // Sabit marka renkleri (logo, degrade, koyu zemin üstünde beyaz yazı).
  static const navyBase = Color(0xFF26304E);
  static const burgundyBase = Color(0xFF8E2235);
  static const navyDark = Color(0xFF1B2238);
  static const burgundyDark = Color(0xFF6F1A29);
  static const gradient = LinearGradient(colors: [navyBase, burgundyBase]);

  /// Başlık / ikon rengi (koyu modda açık lacivert).
  static Color get navy => dark ? const Color(0xFFB7C1E4) : navyBase;

  /// Vurgu rengi (koyu modda açık bordo-gül).
  static Color get burgundy => dark ? const Color(0xFFE88A9B) : burgundyBase;
  static Color get paper => dark ? const Color(0xFF13151C) : const Color(0xFFF7F6F0);
  static Color get paper2 => dark ? const Color(0xFF252935) : const Color(0xFFEFECE2);
  static Color get line => dark ? const Color(0xFF363B4B) : const Color(0xFFE3DFD2);
  static Color get ink => dark ? const Color(0xFFE7E8EE) : const Color(0xFF232A3D);
  static Color get muted => dark ? const Color(0xFFA0A5B3) : const Color(0xFF686D7A);
  static Color get success => dark ? const Color(0xFF74C796) : const Color(0xFF2E7D4F);

  /// Kart / alan zemini (açıkta beyaz).
  static Color get card => dark ? const Color(0xFF1C1F28) : Colors.white;

  /// Açık zeminli rozet / uyarı kutularının koyu karşılığı: açık renk [light], koyuda [darkColor].
  static Color tint(Color light, Color darkColor) => dark ? darkColor : light;

  /// Uyarı (sarı) kutusu zemini, kenarı ve yazısı.
  static Color get warnBg => tint(const Color(0xFFFFF7E6), const Color(0xFF3A2F18));
  static Color get warnBorder => tint(const Color(0xFFF1D9A6), const Color(0xFF6B5626));
  static Color get warnText => tint(const Color(0xFF6B4A00), const Color(0xFFF0D59A));
  static Color get warnTitle => tint(const Color(0xFF8A5A00), const Color(0xFFF5C76B));
}

ThemeData buildTheme(Brightness brightness) {
  Brand.dark = brightness == Brightness.dark;
  final dark = Brand.dark;
  final scheme = ColorScheme.fromSeed(
    seedColor: Brand.burgundyBase,
    brightness: brightness,
    primary: dark ? const Color(0xFFC9475E) : Brand.burgundyBase,
    onPrimary: Colors.white,
    secondary: Brand.navy,
    onSecondary: dark ? Brand.navyDark : Colors.white,
    surface: Brand.card,
    onSurface: Brand.ink,
    error: dark ? const Color(0xFFF2B8B5) : const Color(0xFFB3261E),
  );

  OutlineInputBorder border(Color c, [double w = 1]) =>
      OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: c, width: w));

  return ThemeData(
    useMaterial3: true,
    brightness: brightness,
    colorScheme: scheme,
    scaffoldBackgroundColor: Brand.paper,
    appBarTheme: AppBarTheme(
      backgroundColor: Brand.paper,
      foregroundColor: Brand.navy,
      elevation: 0,
      scrolledUnderElevation: 1,
      centerTitle: false,
      titleTextStyle: TextStyle(color: Brand.navy, fontSize: 20, fontWeight: FontWeight.w700),
    ),
    cardTheme: CardThemeData(
      color: Brand.card,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: BorderSide(color: Brand.line)),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Brand.card,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      border: border(Brand.line),
      enabledBorder: border(Brand.line),
      focusedBorder: border(Brand.burgundy, 1.6),
      errorBorder: border(scheme.error),
      focusedErrorBorder: border(scheme.error, 1.6),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size(64, 50),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        minimumSize: const Size(64, 48),
        foregroundColor: Brand.navy,
        side: BorderSide(color: Brand.line),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    ),
    textButtonTheme: TextButtonThemeData(style: TextButton.styleFrom(foregroundColor: Brand.burgundy)),
    chipTheme: ChipThemeData(
      side: BorderSide(color: Brand.line),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      selectedColor: Brand.burgundy.withValues(alpha: dark ? 0.22 : 0.12),
      checkmarkColor: Brand.burgundy,
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: Brand.card,
      indicatorColor: Brand.burgundy.withValues(alpha: dark ? 0.22 : 0.12),
      labelTextStyle: WidgetStateProperty.resolveWith((s) => TextStyle(
          fontSize: 12,
          fontWeight: s.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
          color: s.contains(WidgetState.selected) ? Brand.burgundy : Brand.muted)),
      iconTheme: WidgetStateProperty.resolveWith(
          (s) => IconThemeData(color: s.contains(WidgetState.selected) ? Brand.burgundy : Brand.muted)),
    ),
    floatingActionButtonTheme: FloatingActionButtonThemeData(
      backgroundColor: scheme.primary,
      foregroundColor: Colors.white,
    ),
    dialogTheme: DialogThemeData(backgroundColor: Brand.card),
    bottomSheetTheme: BottomSheetThemeData(backgroundColor: Brand.card),
    dividerTheme: DividerThemeData(color: Brand.line, space: 1),
    snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
  );
}
