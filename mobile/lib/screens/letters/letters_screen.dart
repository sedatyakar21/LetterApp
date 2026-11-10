import 'package:flutter/material.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';
import 'letter_detail_screen.dart';
import 'letter_editor_screen.dart';

class LettersScreen extends StatefulWidget {
  const LettersScreen({super.key});

  @override
  State<LettersScreen> createState() => _LettersScreenState();
}

class _LettersScreenState extends State<LettersScreen> {
  Future<List<LetterSummary>>? _future;
  int _version = -1;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Ödeme, kayıt veya iptal sonrası Session.lettersChanged() listeyi yeniler.
    final session = AppScope.of(context);
    if (session.lettersVersion != _version) {
      _version = session.lettersVersion;
      _future = session.api.letters();
    }
  }

  Future<void> _reload() async {
    final f = AppScope.read(context).api.letters();
    setState(() {
      _future = f;
    });
    await f.catchError((_) => <LetterSummary>[]);
  }

  Future<void> _open(String id, {bool checkout = false}) async {
    await Navigator.of(context)
        .push(MaterialPageRoute(builder: (_) => LetterDetailScreen(letterId: id, openCheckout: checkout)));
  }

  Future<void> _write() async {
    final outcome =
        await Navigator.of(context).push<EditorOutcome>(MaterialPageRoute(builder: (_) => const LetterEditorScreen()));
    if (outcome != null && mounted) await _open(outcome.letter.id, checkout: outcome.checkout);
  }

  @override
  Widget build(BuildContext context) {
    final me = AppScope.of(context).me;
    return Scaffold(
      appBar: AppBar(
        title: Text(me == null ? 'Mektuplarım' : 'Merhaba ${me.firstName} 👋'),
        actions: [IconButton(tooltip: 'Yenile', onPressed: _reload, icon: const Icon(Icons.refresh))],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _write,
        icon: const Icon(Icons.edit_outlined),
        label: const Text('Mektup Yaz'),
      ),
      body: RefreshIndicator(
        onRefresh: _reload,
        child: AsyncView<List<LetterSummary>>(
          future: _future,
          onRetry: _reload,
          builder: (context, letters) {
            if (letters.isEmpty) {
              return ListView(children: [
                SizedBox(
                  height: MediaQuery.of(context).size.height * 0.6,
                  child: EmptyView(
                    icon: Icons.drafts_outlined,
                    title: 'Henüz mektubunuz yok',
                    message: 'İlk mektubunuzu yazın; biz basıp zarflayalım ve kayıtlı postayla gönderelim.',
                    action: FilledButton.icon(onPressed: _write, icon: const Icon(Icons.edit), label: const Text('Mektup Yaz')),
                  ),
                ),
              ]);
            }
            return ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 96),
              itemCount: letters.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (context, i) => _LetterTile(letter: letters[i], onTap: () => _open(letters[i].id)),
            );
          },
        ),
      ),
    );
  }
}

class _LetterTile extends StatelessWidget {
  const _LetterTile({required this.letter, required this.onTap});
  final LetterSummary letter;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final icon = switch (letter.recipientType) {
      'Soldier' => Icons.military_tech_outlined,
      'Inmate' => Icons.account_balance_outlined,
      _ => Icons.favorite_border,
    };
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            CircleAvatar(
              backgroundColor: Brand.paper2,
              foregroundColor: Brand.burgundy,
              child: Icon(icon, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Row(children: [
                  Expanded(
                    child: Text(letter.recipientName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
                  ),
                  StatusChip(status: letter.status, label: letter.statusLabel),
                ]),
                const SizedBox(height: 4),
                Text(
                    letter.preview.isEmpty
                        ? '(Mektup metni henüz yazılmadı)'
                        : letter.preview.replaceAll(RegExp(r'\s+'), ' '),
                    maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(color: Brand.muted)),
                const SizedBox(height: 6),
                Row(children: [
                  Text(dateText(letter.updatedAt), style: const TextStyle(color: Brand.muted, fontSize: 12)),
                  if (letter.trackingCode != null) ...[
                    const Spacer(),
                    const Icon(Icons.local_shipping_outlined, size: 14, color: Brand.muted),
                    const SizedBox(width: 4),
                    Text(letter.trackingCode!, style: const TextStyle(color: Brand.muted, fontSize: 12)),
                  ],
                ]),
              ]),
            ),
          ]),
        ),
      ),
    );
  }
}
