import 'package:flutter/material.dart';

/// Mektup önizlemesindeki renkler ve yazı stilleri (sitedeki site.css ile aynı).
class LetterLook {
  static const paper = {
    'White': Color(0xFFFFFFFF),
    'Cream': Color(0xFFFBF3DF),
    'Pink': Color(0xFFFDE4EA),
    'Blue': Color(0xFFE3EEFB),
    'Green': Color(0xFFE5F4E3),
  };

  static const envelope = {
    'White': Color(0xFFFFFFFF),
    'Kraft': Color(0xFFC8A27A),
    'Red': Color(0xFFB3263A),
    'Navy': Color(0xFF26304E),
    'Pink': Color(0xFFF4B6C6),
  };

  static const ink = {
    'Black': Color(0xFF222222),
    'Navy': Color(0xFF1F3A68),
    'Burgundy': Color(0xFF7A1F33),
  };

  static const postcardGradients = {
    1: [Color(0xFFFFD1DC), Color(0xFFFF9AA2)],
    2: [Color(0xFFB5EAD7), Color(0xFF74C69D)],
    3: [Color(0xFFC7CEEA), Color(0xFF8E9AAF)],
    4: [Color(0xFFFFDAC1), Color(0xFFF9C74F)],
  };

  static const postcardIcons = {
    'Love': '❤️',
    'Nature': '🌲',
    'Cities': '🏙️',
    'Celebration': '🎉',
    'Motivation': '⭐',
  };

  static const scentIcons = {
    'None': '—',
    'Magnolia': '🌸',
    'Lavender': '💜',
    'SpringFlowers': '🌼',
  };

  static Color paperColor(String v) => paper[v] ?? Colors.white;
  static Color inkColor(String v) => ink[v] ?? const Color(0xFF222222);

  static TextStyle textStyle(String font, String inkName) {
    final color = inkColor(inkName);
    return switch (font) {
      // Sistem el yazısı fontu: Android "cursive" (Dancing Script), iOS Snell Roundhand.
      // Yapay italik kullanılmaz; bazı cihazlarda "ı" harfi yanlış çiziliyor.
      'Handwriting' => TextStyle(
          color: color, fontSize: 21, height: 1.45, fontFamily: 'cursive', fontFamilyFallback: const ['Snell Roundhand', 'Segoe Script']),
      'Sans' => TextStyle(color: color, fontSize: 15, height: 1.6),
      _ => TextStyle(color: color, fontSize: 16, height: 1.6, fontFamily: 'serif', fontFamilyFallback: const ['Georgia', 'Times New Roman']),
    };
  }
}

/// Küçük renk örneği (seçim çiplerinde).
class Swatch extends StatelessWidget {
  const Swatch(this.color, {super.key, this.size = 18});
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          color: color,
          shape: BoxShape.circle,
          border: Border.all(color: Colors.black.withValues(alpha: 0.15)),
        ),
      );
}

/// Kartpostal tasarımı küçük resmi.
class PostcardTile extends StatelessWidget {
  const PostcardTile({super.key, required this.category, required this.design, this.selected = false, this.size = 64});
  final String category;
  final int design;
  final bool selected;
  final double size;

  @override
  Widget build(BuildContext context) {
    final colors = LetterLook.postcardGradients[design] ?? LetterLook.postcardGradients[1]!;
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      width: size * 1.4,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(colors: colors, begin: Alignment.topLeft, end: Alignment.bottomRight),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
            color: selected ? Theme.of(context).colorScheme.primary : Colors.white, width: selected ? 3 : 2),
        boxShadow: const [BoxShadow(color: Color(0x1A000000), blurRadius: 4, offset: Offset(0, 2))],
      ),
      alignment: Alignment.center,
      child: Text(LetterLook.postcardIcons[category] ?? '', style: TextStyle(fontSize: size * 0.4)),
    );
  }
}
