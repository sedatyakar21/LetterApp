import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../api/api_client.dart';
import 'theme.dart';

final _money = NumberFormat.currency(locale: 'tr_TR', symbol: '₺', decimalDigits: 2);
final _number = NumberFormat('#,##0.##', 'tr_TR');

String money(double v) => _money.format(v);
String number(double v) => _number.format(v);
String dateText(DateTime d) => DateFormat('d MMMM yyyy', 'tr_TR').format(d);
String dateTimeText(DateTime d) => DateFormat('d MMM yyyy HH:mm', 'tr_TR').format(d);

String errorText(Object e) => e is ApiException ? e.message : 'Beklenmeyen bir hata oluştu.';

void showMessage(BuildContext context, String text, {bool error = false}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(text),
      backgroundColor: error ? Theme.of(context).colorScheme.error : Brand.navy,
    ));
}

Future<bool> confirm(BuildContext context, {required String title, required String message, String ok = 'Evet', bool danger = false}) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (c) => AlertDialog(
      title: Text(title),
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Vazgeç')),
        FilledButton(
          style: danger ? FilledButton.styleFrom(backgroundColor: Theme.of(c).colorScheme.error) : null,
          onPressed: () => Navigator.pop(c, true),
          child: Text(ok),
        ),
      ],
    ),
  );
  return result ?? false;
}

/// Yükleniyor / hata (tekrar dene) / içerik.
class AsyncView<T> extends StatelessWidget {
  const AsyncView({super.key, required this.future, required this.builder, this.onRetry});
  final Future<T>? future;
  final Widget Function(BuildContext, T) builder;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<T>(
      future: future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snap.hasError) return ErrorView(message: errorText(snap.error!), onRetry: onRetry);
        return builder(context, snap.data as T);
      },
    );
  }
}

class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, this.onRetry});
  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const Icon(Icons.cloud_off_outlined, size: 48, color: Brand.muted),
          const SizedBox(height: 12),
          Text(message, textAlign: TextAlign.center, style: const TextStyle(color: Brand.muted)),
          if (onRetry != null) ...[
            const SizedBox(height: 16),
            OutlinedButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('Tekrar dene')),
          ],
        ]),
      ),
    );
  }
}

class EmptyView extends StatelessWidget {
  const EmptyView({super.key, required this.icon, required this.title, this.message, this.action});
  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Icon(icon, size: 56, color: Brand.burgundy.withValues(alpha: 0.6)),
          const SizedBox(height: 16),
          Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700)),
          if (message != null) ...[
            const SizedBox(height: 8),
            Text(message!, textAlign: TextAlign.center, style: const TextStyle(color: Brand.muted)),
          ],
          if (action != null) ...[const SizedBox(height: 20), action!],
        ]),
      ),
    );
  }
}

/// Başlıklı beyaz kart.
class SectionCard extends StatelessWidget {
  const SectionCard({super.key, this.title, this.trailing, required this.child, this.padding = const EdgeInsets.all(16)});
  final String? title;
  final Widget? trailing;
  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: padding,
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          if (title != null) ...[
            Row(children: [
              Expanded(
                child: Text(title!,
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: Brand.navy)),
              ),
              ?trailing,
            ]),
            const SizedBox(height: 12),
          ],
          child,
        ]),
      ),
    );
  }
}

/// Mektup durumu rozeti.
class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.status, required this.label});
  final String status;
  final String label;

  static (Color, Color) colors(String status) => switch (status) {
        'Draft' => (const Color(0xFFECEAE3), Brand.muted),
        'AwaitingPayment' => (const Color(0xFFFFF1D6), const Color(0xFF8A5A00)),
        'Paid' || 'InProduction' => (const Color(0xFFE3E6EF), Brand.navy),
        'Shipped' => (const Color(0xFFE1EEFB), const Color(0xFF1F4F8A)),
        'Delivered' => (const Color(0xFFE3F2E6), Brand.success),
        'Cancelled' => (const Color(0xFFF8E1E4), Brand.burgundy),
        _ => (Brand.paper2, Brand.muted),
      };

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = colors(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(20)),
      child: Text(label, style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w700)),
    );
  }
}

/// Kart içindeki "etiket ...... değer" satırı.
class InfoRow extends StatelessWidget {
  const InfoRow(this.label, this.value, {super.key, this.bold = false, this.valueColor});
  final String label;
  final String value;
  final bool bold;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final style = TextStyle(fontWeight: bold ? FontWeight.w700 : FontWeight.w400, fontSize: bold ? 16 : 14);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Expanded(child: Text(label, style: style.copyWith(color: bold ? Brand.ink : Brand.muted))),
        const SizedBox(width: 12),
        Text(value, style: style.copyWith(color: valueColor ?? Brand.ink)),
      ]),
    );
  }
}

/// Mektup durum geçmişi (zaman çizelgesi).
class Timeline extends StatelessWidget {
  const Timeline({super.key, required this.items});
  final List<({String title, String note, DateTime at, String status})> items;

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      for (var i = 0; i < items.length; i++)
        IntrinsicHeight(
          child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            SizedBox(
              width: 24,
              child: Column(children: [
                Container(
                  width: 14,
                  height: 14,
                  margin: const EdgeInsets.only(top: 3),
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: i == 0 ? StatusChip.colors(items[i].status).$2 : Colors.white,
                    border: Border.all(color: StatusChip.colors(items[i].status).$2, width: 2),
                  ),
                ),
                if (i < items.length - 1) Expanded(child: Container(width: 2, color: Brand.line)),
              ]),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.only(bottom: 16),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(items[i].title, style: const TextStyle(fontWeight: FontWeight.w700)),
                  if (items[i].note.isNotEmpty) Text(items[i].note, style: const TextStyle(color: Brand.ink)),
                  Text(dateTimeText(items[i].at), style: const TextStyle(color: Brand.muted, fontSize: 12)),
                ]),
              ),
            ),
          ]),
        ),
    ]);
  }
}

class Stars extends StatelessWidget {
  const Stars({super.key, required this.rating, this.size = 18});
  final double rating;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Row(mainAxisSize: MainAxisSize.min, children: [
      for (var i = 1; i <= 5; i++)
        Icon(
          rating >= i ? Icons.star_rounded : (rating >= i - 0.5 ? Icons.star_half_rounded : Icons.star_outline_rounded),
          size: size,
          color: const Color(0xFFE0A100),
        ),
    ]);
  }
}

/// Gradyanlı marka başlığı (giriş ekranı, hesap kartı).
class BrandHeader extends StatelessWidget {
  const BrandHeader({super.key, required this.child, this.padding = const EdgeInsets.all(20)});
  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(gradient: Brand.gradient, borderRadius: BorderRadius.circular(18)),
      child: DefaultTextStyle.merge(style: const TextStyle(color: Colors.white), child: child),
    );
  }
}
