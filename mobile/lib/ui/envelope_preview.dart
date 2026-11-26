import 'package:flutter/material.dart';

import 'letter_style.dart';

/// Zarf önizlemesi: yönetim panelindeki zarf çıktısının (13×18 cm, 180×130 mm) aynı düzeni.
/// Sol üst gönderen kutusu, sağ üst tahmini ağırlık + barkod alanı, sağ alt alıcı kutusu, sol alt aracı firma + logo.
/// Ölçüler zarf genişliğine göre milimetreden ölçeklenir.
class EnvelopePreview extends StatelessWidget {
  const EnvelopePreview({
    super.key,
    required this.envelope,
    required this.senderName,
    required this.senderLine,
    required this.senderPlace,
    required this.recipientName,
    required this.recipientLine,
    required this.recipientPlace,
    this.recipientNote,
    this.recipientDetail,
    this.weightGrams,
  });

  /// Zarf rengi (White, Kraft, Red, Navy, Pink).
  final String envelope;
  final String senderName, senderLine, senderPlace;
  final String recipientName, recipientLine, recipientPlace;
  final String? recipientNote, recipientDetail;
  final int? weightGrams;

  static const _navy = Color(0xFF26304E);
  static const _burgundy = Color(0xFF8E2235);
  static const intermediary = 'Aracı Firma : BiMektubum';

  @override
  Widget build(BuildContext context) {
    final dark = envelope == 'Red' || envelope == 'Navy';
    final ink = dark ? Colors.white : Colors.black;
    return AspectRatio(
      aspectRatio: 180 / 130,
      child: LayoutBuilder(builder: (context, c) {
        final mm = c.maxWidth / 180; // 1 mm'nin ekrandaki karşılığı
        final base = 3.53 * mm; // 10 pt
        TextStyle t({double scale = 1.1, FontWeight? weight, Color? color, double? spacing}) => TextStyle(
            fontSize: base * scale, height: 1.3, fontWeight: weight, color: color ?? ink, letterSpacing: spacing);

        Widget box({required String caption, required Color color, required List<Widget> lines}) => Container(
              padding: EdgeInsets.symmetric(horizontal: 4 * mm, vertical: 2 * mm),
              decoration: BoxDecoration(
                border: Border.all(color: dark ? Colors.white : color, width: .4 * mm),
                borderRadius: BorderRadius.only(topLeft: Radius.circular(5 * mm), bottomRight: Radius.circular(5 * mm)),
              ),
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
                Text(caption, style: t(scale: .88, weight: FontWeight.w700, color: dark ? Colors.white : color, spacing: .1 * base)),
                ...lines,
              ]),
            );

        Text line(String text, {FontWeight? weight, int maxLines = 2}) =>
            Text(text, maxLines: maxLines, overflow: TextOverflow.ellipsis, style: t(weight: weight));

        return Container(
          decoration: BoxDecoration(
            color: LetterLook.envelope[envelope] ?? Colors.white,
            borderRadius: BorderRadius.circular(4),
            border: Border.all(color: const Color(0xFFE3DFD2)),
            boxShadow: const [BoxShadow(color: Color(0x24000000), blurRadius: 10, offset: Offset(0, 2))],
          ),
          child: Stack(children: [
            Positioned(
              top: 8 * mm,
              left: 10 * mm,
              child: ConstrainedBox(
                constraints: BoxConstraints(maxWidth: c.maxWidth * .45),
                child: box(caption: 'GÖNDEREN', color: _navy, lines: [
                  line(senderName.isEmpty ? 'Gönderen adı' : senderName, weight: FontWeight.w600, maxLines: 1),
                  if (senderLine.isNotEmpty) line(senderLine),
                  if (senderPlace.isNotEmpty) line(senderPlace, maxLines: 1),
                ]),
              ),
            ),
            Positioned(
              top: 8 * mm,
              right: 10 * mm,
              child: Column(children: [
                Text.rich(TextSpan(text: 'Tahmini ağırlık: ', children: [
                  TextSpan(text: weightGrams == null ? '—' : '$weightGrams g', style: const TextStyle(fontWeight: FontWeight.w700)),
                ]), style: t(scale: .9)),
                SizedBox(height: 1.5 * mm),
                Container(
                  width: 50 * mm,
                  padding: EdgeInsets.symmetric(vertical: 1.5 * mm),
                  decoration: BoxDecoration(
                    border: Border.all(color: ink.withValues(alpha: .5), width: .3 * mm),
                    borderRadius: BorderRadius.circular(1.5 * mm),
                  ),
                  child: Text('Barkod ve takip no\npostaya verilince basılır',
                      textAlign: TextAlign.center, style: t(scale: .72, color: ink.withValues(alpha: .75))),
                ),
              ]),
            ),
            Positioned(
              right: 12 * mm,
              bottom: 12 * mm,
              child: ConstrainedBox(
                constraints: BoxConstraints(maxWidth: c.maxWidth * .6),
                child: box(caption: 'ALICI', color: _burgundy, lines: [
                  line(recipientName.isEmpty ? 'Alıcı adı' : recipientName, weight: FontWeight.w600, maxLines: 1),
                  if ((recipientNote ?? '').isNotEmpty) line(recipientNote!, maxLines: 1),
                  if ((recipientDetail ?? '').isNotEmpty) line(recipientDetail!),
                  if (recipientLine.isNotEmpty) line(recipientLine),
                  if (recipientPlace.isNotEmpty) line(recipientPlace, weight: FontWeight.w600, maxLines: 1),
                ]),
              ),
            ),
            Positioned(
              left: 10 * mm,
              bottom: 8 * mm,
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(intermediary, style: t(scale: 1, weight: FontWeight.w700)),
                SizedBox(height: 1.5 * mm),
                Image.asset('assets/images/logo.png', height: 32 * mm),
              ]),
            ),
          ]),
        );
      }),
    );
  }
}
