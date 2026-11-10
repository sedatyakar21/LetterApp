import 'package:flutter/material.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

/// Onaylanmış müşteri yorumları (sayfalı).
class ReviewsScreen extends StatefulWidget {
  const ReviewsScreen({super.key});

  @override
  State<ReviewsScreen> createState() => _ReviewsScreenState();
}

class _ReviewsScreenState extends State<ReviewsScreen> {
  final _items = <Review>[];
  ReviewPage? _last;
  String? _error;
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _loadMore();
  }

  Future<void> _loadMore() async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await AppScope.read(context).api.reviews(page: (_last?.page ?? 0) + 1);
      setState(() {
        _last = page;
        _items.addAll(page.items);
      });
    } catch (e) {
      setState(() => _error = errorText(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final last = _last;
    return Scaffold(
      appBar: AppBar(title: const Text('Müşteri Yorumları')),
      body: last == null
          ? (_error != null ? ErrorView(message: _error!, onRetry: _loadMore) : const Center(child: CircularProgressIndicator()))
          : ListView(padding: const EdgeInsets.all(16), children: [
              SectionCard(
                child: Row(children: [
                  Text(last.average.toStringAsFixed(1).replaceAll('.', ','),
                      style: const TextStyle(fontSize: 36, fontWeight: FontWeight.w800, color: Brand.navy)),
                  const SizedBox(width: 12),
                  Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Stars(rating: last.average, size: 22),
                    Text('${last.count} değerlendirme', style: const TextStyle(color: Brand.muted)),
                  ]),
                ]),
              ),
              const SizedBox(height: 12),
              if (_items.isEmpty)
                const EmptyView(icon: Icons.reviews_outlined, title: 'Henüz yorum yok'),
              for (final r in _items) ...[
                SectionCard(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Row(children: [
                      Stars(rating: r.rating.toDouble()),
                      const Spacer(),
                      Text(dateText(r.createdAt), style: const TextStyle(color: Brand.muted, fontSize: 12)),
                    ]),
                    const SizedBox(height: 8),
                    Text(r.comment),
                    const SizedBox(height: 8),
                    Text('${r.authorName}${r.recipientCity == null ? '' : ' · 📍 Alıcı: ${r.recipientCity}'}',
                        style: const TextStyle(color: Brand.muted, fontSize: 13, fontWeight: FontWeight.w600)),
                  ]),
                ),
                const SizedBox(height: 10),
              ],
              if (last.hasNext)
                Center(
                  child: _loading
                      ? const CircularProgressIndicator()
                      : OutlinedButton(onPressed: _loadMore, child: const Text('Daha fazla yorum')),
                ),
            ]),
    );
  }
}
