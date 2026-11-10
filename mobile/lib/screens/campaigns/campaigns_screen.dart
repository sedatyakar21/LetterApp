import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

class CampaignsScreen extends StatefulWidget {
  const CampaignsScreen({super.key});

  @override
  State<CampaignsScreen> createState() => _CampaignsScreenState();
}

class _CampaignsScreenState extends State<CampaignsScreen> {
  Future<List<CampaignCard>>? _future;

  @override
  void initState() {
    super.initState();
    _future = AppScope.read(context).api.campaigns();
  }

  Future<void> _reload() async {
    final f = AppScope.read(context).api.campaigns();
    setState(() {
      _future = f;
    });
    await f.catchError((_) => <CampaignCard>[]);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Kampanyalar')),
      body: RefreshIndicator(
        onRefresh: _reload,
        child: AsyncView<List<CampaignCard>>(
          future: _future,
          onRetry: _reload,
          builder: (context, items) => items.isEmpty
              ? ListView(children: const [
                  SizedBox(height: 120),
                  EmptyView(
                      icon: Icons.local_offer_outlined,
                      title: 'Şu an aktif kampanya yok',
                      message: 'Yeni kampanyalar başladığında burada göreceksiniz.'),
                ])
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) => CampaignTile(
                    campaign: items[i],
                    onTap: () => Navigator.of(context)
                        .push(MaterialPageRoute(builder: (_) => CampaignDetailScreen(slug: items[i].slug, title: items[i].title))),
                  ),
                ),
        ),
      ),
    );
  }
}

class CampaignTile extends StatelessWidget {
  const CampaignTile({super.key, required this.campaign, this.onTap});
  final CampaignCard campaign;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final c = campaign;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          _Cover(campaign: c, height: 130),
          Padding(
            padding: const EdgeInsets.all(14),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                _StatusBadge(campaign: c),
                const Spacer(),
                if (c.couponCode != null) _CouponPill(code: c.couponCode!),
              ]),
              const SizedBox(height: 8),
              Text(c.title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: Brand.navy)),
              const SizedBox(height: 4),
              Text(c.summary, style: const TextStyle(color: Brand.ink)),
              const SizedBox(height: 8),
              Text(c.dateRange, style: const TextStyle(color: Brand.muted, fontSize: 12)),
              if (c.audienceText != null)
                Text(c.audienceText!, style: const TextStyle(color: Brand.burgundy, fontSize: 12, fontWeight: FontWeight.w600)),
            ]),
          ),
        ]),
      ),
    );
  }
}

class CampaignDetailScreen extends StatelessWidget {
  const CampaignDetailScreen({super.key, required this.slug, required this.title});
  final String slug;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: AsyncView<CampaignCard>(
        future: AppScope.read(context).api.campaign(slug),
        builder: (context, c) => ListView(padding: const EdgeInsets.all(16), children: [
          ClipRRect(borderRadius: BorderRadius.circular(16), child: _Cover(campaign: c, height: 180)),
          const SizedBox(height: 14),
          Row(children: [_StatusBadge(campaign: c), const SizedBox(width: 8), Expanded(child: Text(c.dateRange, style: const TextStyle(color: Brand.muted)))]),
          const SizedBox(height: 10),
          Text(c.title, style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700, color: Brand.navy)),
          const SizedBox(height: 6),
          Text(c.summary, style: const TextStyle(fontSize: 16)),
          if (c.audienceText != null) ...[
            const SizedBox(height: 8),
            Text(c.audienceText!, style: const TextStyle(color: Brand.burgundy, fontWeight: FontWeight.w600)),
          ],
          if (c.minOrderAmount != null) ...[
            const SizedBox(height: 4),
            Text('En az ${money(c.minOrderAmount!)} tutarındaki siparişlerde geçerlidir.', style: const TextStyle(color: Brand.muted)),
          ],
          if (c.couponCode != null) ...[
            const SizedBox(height: 16),
            SectionCard(
              child: Row(children: [
                const Icon(Icons.confirmation_number_outlined, color: Brand.burgundy),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    const Text('Kupon kodu', style: TextStyle(color: Brand.muted, fontSize: 12)),
                    Text(c.couponCode!, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18, letterSpacing: 1)),
                  ]),
                ),
                TextButton.icon(
                  onPressed: () {
                    Clipboard.setData(ClipboardData(text: c.couponCode!));
                    showMessage(context, 'Kupon kodu kopyalandı. Ödeme ekranında kullanabilirsiniz.');
                  },
                  icon: const Icon(Icons.copy, size: 18),
                  label: const Text('Kopyala'),
                ),
              ]),
            ),
          ] else if (c.isActive) ...[
            const SizedBox(height: 12),
            const Text('Kod gerekmez: koşulları sağlayan siparişlerde indirim ödeme ekranında otomatik uygulanır.',
                style: TextStyle(color: Brand.success)),
          ],
          if ((c.description ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 16),
            SimpleMarkdown(c.description!),
          ],
        ]),
      ),
    );
  }
}

/// Kampanya açıklamasındaki basit Markdown'ı (başlık, liste, kalın) düz metin olarak gösterir.
class SimpleMarkdown extends StatelessWidget {
  const SimpleMarkdown(this.text, {super.key});
  final String text;

  static final _bold = RegExp(r'\*\*(.+?)\*\*');

  List<TextSpan> _inline(String s) {
    final spans = <TextSpan>[];
    var last = 0;
    for (final m in _bold.allMatches(s)) {
      if (m.start > last) spans.add(TextSpan(text: s.substring(last, m.start)));
      spans.add(TextSpan(text: m.group(1), style: const TextStyle(fontWeight: FontWeight.w700)));
      last = m.end;
    }
    if (last < s.length) spans.add(TextSpan(text: s.substring(last)));
    return spans;
  }

  @override
  Widget build(BuildContext context) {
    final widgets = <Widget>[];
    for (final raw in text.replaceAll('\r', '').split('\n')) {
      final line = raw.trimRight();
      if (line.trim().isEmpty) {
        widgets.add(const SizedBox(height: 8));
      } else if (line.startsWith('#')) {
        widgets.add(Padding(
          padding: const EdgeInsets.only(top: 6, bottom: 4),
          child: Text(line.replaceFirst(RegExp(r'^#+\s*'), ''),
              style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: Brand.navy)),
        ));
      } else if (RegExp(r'^\s*([-*]|\d+\.)\s+').hasMatch(line)) {
        final bullet = RegExp(r'^\s*(\d+\.)').firstMatch(line)?.group(1) ?? '•';
        widgets.add(Padding(
          padding: const EdgeInsets.only(left: 4, bottom: 4),
          child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            SizedBox(width: 22, child: Text(bullet)),
            Expanded(
                child: Text.rich(TextSpan(children: _inline(line.replaceFirst(RegExp(r'^\s*([-*]|\d+\.)\s+'), ''))))),
          ]),
        ));
      } else {
        widgets.add(Text.rich(TextSpan(children: _inline(line)), style: const TextStyle(height: 1.5)));
      }
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: widgets);
  }
}

class _Cover extends StatelessWidget {
  const _Cover({required this.campaign, required this.height});
  final CampaignCard campaign;
  final double height;

  @override
  Widget build(BuildContext context) {
    final placeholder = Container(
      height: height,
      decoration: const BoxDecoration(
          gradient: LinearGradient(colors: [Color(0xFFF1EEE4), Color(0xFFF6EEF0)], begin: Alignment.topLeft, end: Alignment.bottomRight)),
      alignment: Alignment.center,
      child: campaign.badgeText != null
          ? Text(campaign.badgeText!,
              textAlign: TextAlign.center, style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w800, color: Brand.burgundy))
          : const Icon(Icons.local_offer_outlined, size: 40, color: Brand.burgundy),
    );
    if (campaign.coverUrl == null) return placeholder;
    return Image.network(campaign.coverUrl!,
        height: height, width: double.infinity, fit: BoxFit.cover, errorBuilder: (_, _, _) => placeholder);
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.campaign});
  final CampaignCard campaign;

  @override
  Widget build(BuildContext context) {
    final urgent = campaign.isActive && (campaign.statusText.startsWith('Son') || campaign.statusText.startsWith('Bugün'));
    final (bg, fg) = campaign.isActive
        ? (urgent ? (const Color(0xFFF8E1E4), Brand.burgundy) : (const Color(0xFFE3F2E6), Brand.success))
        : (const Color(0xFFE3E6EF), Brand.navy);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(20)),
      child: Text(campaign.statusText, style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w700)),
    );
  }
}

class _CouponPill extends StatelessWidget {
  const _CouponPill({required this.code});
  final String code;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(
          border: Border.all(color: Brand.burgundy, style: BorderStyle.solid),
          borderRadius: BorderRadius.circular(6),
        ),
        child: Text(code, style: const TextStyle(color: Brand.burgundy, fontWeight: FontWeight.w700, fontSize: 12, letterSpacing: 0.5)),
      );
}
