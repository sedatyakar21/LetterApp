import 'package:flutter/material.dart';

/// Sitedeki marka renkleri (site.css).
class Brand {
  static const navy = Color(0xFF26304E);
  static const navyDark = Color(0xFF1B2238);
  static const burgundy = Color(0xFF8E2235);
  static const burgundyDark = Color(0xFF6F1A29);
  static const paper = Color(0xFFF7F6F0);
  static const paper2 = Color(0xFFEFECE2);
  static const line = Color(0xFFE3DFD2);
  static const ink = Color(0xFF232A3D);
  static const muted = Color(0xFF686D7A);
  static const success = Color(0xFF2E7D4F);

  static const gradient = LinearGradient(colors: [navy, burgundy]);
}

ThemeData buildTheme() {
  final scheme = ColorScheme.fromSeed(
    seedColor: Brand.burgundy,
    primary: Brand.burgundy,
    onPrimary: Colors.white,
    secondary: Brand.navy,
    onSecondary: Colors.white,
    surface: Colors.white,
    onSurface: Brand.ink,
    error: const Color(0xFFB3261E),
  );

  OutlineInputBorder border(Color c, [double w = 1]) =>
      OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: c, width: w));

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: Brand.paper,
    appBarTheme: const AppBarTheme(
      backgroundColor: Brand.paper,
      foregroundColor: Brand.navy,
      elevation: 0,
      scrolledUnderElevation: 1,
      centerTitle: false,
      titleTextStyle: TextStyle(color: Brand.navy, fontSize: 20, fontWeight: FontWeight.w700),
    ),
    cardTheme: CardThemeData(
      color: Colors.white,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16), side: const BorderSide(color: Brand.line)),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
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
        side: const BorderSide(color: Brand.line),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
    ),
    chipTheme: ChipThemeData(
      side: const BorderSide(color: Brand.line),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      selectedColor: Brand.burgundy.withValues(alpha: 0.12),
      checkmarkColor: Brand.burgundy,
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: Colors.white,
      indicatorColor: Brand.burgundy.withValues(alpha: 0.12),
      labelTextStyle: WidgetStateProperty.resolveWith((s) => TextStyle(
          fontSize: 12,
          fontWeight: s.contains(WidgetState.selected) ? FontWeight.w700 : FontWeight.w500,
          color: s.contains(WidgetState.selected) ? Brand.burgundy : Brand.muted)),
      iconTheme: WidgetStateProperty.resolveWith(
          (s) => IconThemeData(color: s.contains(WidgetState.selected) ? Brand.burgundy : Brand.muted)),
    ),
    floatingActionButtonTheme: const FloatingActionButtonThemeData(
      backgroundColor: Brand.burgundy,
      foregroundColor: Colors.white,
    ),
    dividerTheme: const DividerThemeData(color: Brand.line, space: 1),
    snackBarTheme: const SnackBarThemeData(behavior: SnackBarBehavior.floating),
  );
}
