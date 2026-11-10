import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/address_fields.dart';
import '../../ui/common.dart';
import '../../ui/letter_style.dart';
import '../../ui/theme.dart';
import 'checkout_screen.dart';
import 'letter_editor_screen.dart';
import 'payment_flow.dart';

class LetterDetailScreen extends StatefulWidget {
  const LetterDetailScreen({super.key, required this.letterId, this.openCheckout = false});
  final String letterId;
  final bool openCheckout;

  @override
  State<LetterDetailScreen> createState() => _LetterDetailScreenState();
}

class _LetterDetailScreenState extends State<LetterDetailScreen> {
  Future<Letter>? _future;
  Letter? _letter;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
    if (widget.openCheckout) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _checkout());
    }
  }

  void _load() {
    final f = AppScope.read(context).api.letter(widget.letterId);
    f.then((l) {
      if (mounted) setState(() => _letter = l);
    }).catchError((_) {});
    // initState'ten çağrıldığında setState gerekmez; sonraki yenilemelerde ekranı günceller.
    if (_future == null) {
      _future = f;
    } else {
      setState(() {
        _future = f;
      });
    }
  }

  void _show(Letter l) {
    setState(() => _letter = l);
    AppScope.read(context).lettersChanged();
  }

  /// İşlem çalışırken düğmeleri kilitler; hata olursa mesaj gösterir.
  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _edit() async {
    final outcome = await Navigator.of(context)
        .push<EditorOutcome>(MaterialPageRoute(builder: (_) => LetterEditorScreen(letter: _letter)));
    if (outcome == null || !mounted) return;
    setState(() => _letter = outcome.letter);
    if (outcome.checkout) await _checkout();
  }

  Future<void> _checkout() async {
    await Navigator.of(context).push<bool>(MaterialPageRoute(builder: (_) => CheckoutScreen(letterId: widget.letterId)));
    if (mounted) _load();
  }

  Future<void> _resumePayment(PendingPayment p) async {
    final (outcome, reason) = await PaymentFlow.complete(context,
        merchantOid: p.merchantOid, amount: p.amount, formUrl: p.formUrl, simulation: p.simulation);
    if (!mounted) return;
    AppScope.read(context).lettersChanged();
    _load();
    if (outcome == PaymentOutcome.succeeded) showMessage(context, 'Ödemeniz alındı! Mektubunuz ilk iş gününde postaya verilecek.');
    if (outcome == PaymentOutcome.failed) showMessage(context, reason ?? 'Ödeme alınamadı.', error: true);
  }

  Future<void> _abandon() async {
    if (!await confirm(context,
        title: 'Ödemeden vazgeçilsin mi?',
        message: 'Mektubunuz tekrar düzenlenebilir hale gelir; ayrılan puanlar iade edilir.',
        ok: 'Vazgeç')) {
      return;
    }
    await _run(() async => _show(await AppScope.read(context).api.abandonPayment(widget.letterId)));
  }

  Future<void> _cancel() async {
    if (!await confirm(context,
        title: 'Mektup iptal edilsin mi?',
        message: 'Ödemeniz iade edilir ve mektup gönderilmez. Bu işlem geri alınamaz.',
        ok: 'İptal et',
        danger: true)) {
      return;
    }
    await _run(() async {
      _show(await AppScope.read(context).api.cancelLetter(widget.letterId));
      if (mounted) showMessage(context, 'Mektubunuz iptal edildi.');
    });
  }

  Future<void> _delete() async {
    if (!await confirm(context,
        title: 'Mektup silinsin mi?', message: 'Taslak kalıcı olarak silinir.', ok: 'Sil', danger: true)) {
      return;
    }
    await _run(() async {
      final session = AppScope.read(context);
      await session.api.deleteLetter(widget.letterId);
      session.lettersChanged();
      if (mounted) Navigator.of(context).pop();
    });
  }

  Future<void> _changeAddress() async {
    final controllers = AddressControllers(_letter!.recipientAddress);
    final form = GlobalKey<FormState>();
    final address = await showModalBottomSheet<Address>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (c) => Padding(
        padding: EdgeInsets.fromLTRB(20, 0, 20, MediaQuery.of(c).viewInsets.bottom + 20),
        child: Form(
          key: form,
          child: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text('Alıcı adresini değiştir', style: Theme.of(c).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
              const SizedBox(height: 4),
              const Text('Mektup postaya verilmeden önce değiştirilebilir.', style: TextStyle(color: Brand.muted)),
              const SizedBox(height: 16),
              AddressFields(controllers: controllers),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () {
                  if (form.currentState!.validate()) Navigator.pop(c, controllers.value);
                },
                child: const Text('Adresi kaydet'),
              ),
            ]),
          ),
        ),
      ),
    );
    controllers.dispose();
    if (address == null || !mounted) return;
    await _run(() async {
      _show(await AppScope.read(context).api.changeAddress(widget.letterId, address));
      if (mounted) showMessage(context, 'Alıcı adresi güncellendi.');
    });
  }

  Future<void> _addPhoto(ImageSource source) async {
    final settings = AppScope.read(context).settings!;
    final file = await ImagePicker().pickImage(source: source, maxWidth: 2400, maxHeight: 2400, imageQuality: 85);
    if (file == null || !mounted) return;
    final bytes = await file.readAsBytes();
    if (!mounted) return;
    if (bytes.length > settings.maxPhotoBytes) {
      showMessage(context, 'Fotoğraf boyutu en fazla ${settings.maxPhotoBytes ~/ (1024 * 1024)} MB olabilir.', error: true);
      return;
    }
    var name = file.name;
    if (!RegExp(r'\.(jpe?g|png|webp)$', caseSensitive: false).hasMatch(name)) name = '$name.jpg';
    await _run(() async {
      _show(await AppScope.read(context).api.addPhoto(widget.letterId, bytes, name));
      if (mounted) showMessage(context, 'Fotoğraf eklendi.');
    });
  }

  Future<void> _removePhoto(Photo p) async {
    if (!await confirm(context, title: 'Fotoğraf kaldırılsın mı?', message: p.fileName, ok: 'Kaldır', danger: true)) return;
    await _run(() async => _show(await AppScope.read(context).api.removePhoto(widget.letterId, p.id)));
  }

  @override
  Widget build(BuildContext context) {
    final l = _letter;
    return Scaffold(
      appBar: AppBar(
        title: Text(l?.recipientName ?? 'Mektup'),
        actions: [
          if (l != null && (l.canChangeAddress || l.canCancel || l.canDelete))
            PopupMenuButton<String>(
              onSelected: (v) => switch (v) {
                'address' => _changeAddress(),
                'cancel' => _cancel(),
                'delete' => _delete(),
                _ => null,
              },
              itemBuilder: (_) => [
                if (l.canChangeAddress) const PopupMenuItem(value: 'address', child: Text('Alıcı adresini değiştir')),
                if (l.canCancel) const PopupMenuItem(value: 'cancel', child: Text('Mektubu iptal et')),
                if (l.canDelete) const PopupMenuItem(value: 'delete', child: Text('Taslağı sil')),
              ],
            ),
        ],
        bottom: _busy ? const PreferredSize(preferredSize: Size.fromHeight(2), child: LinearProgressIndicator(minHeight: 2)) : null,
      ),
      body: l == null
          ? AsyncView<Letter>(future: _future, onRetry: _load, builder: (_, _) => const SizedBox())
          : RefreshIndicator(
              onRefresh: () async {
                _load();
                await _future?.catchError((_) => l);
              },
              child: ListView(padding: const EdgeInsets.fromLTRB(16, 8, 16, 32), children: _sections(l)),
            ),
      bottomNavigationBar: l != null && l.isEditable
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                child: Row(children: [
                  Expanded(
                    child: OutlinedButton.icon(
                        onPressed: _busy ? null : _edit, icon: const Icon(Icons.edit_outlined), label: const Text('Düzenle')),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: FilledButton.icon(
                      onPressed: _busy || l.content.trim().isEmpty ? null : _checkout,
                      icon: const Icon(Icons.lock_outline),
                      label: const Text('Ödemeye Geç'),
                    ),
                  ),
                ]),
              ),
            )
          : null,
    );
  }

  List<Widget> _sections(Letter l) {
    final settings = AppScope.of(context).settings!;
    return [
      // Durum
      SectionCard(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            StatusChip(status: l.status, label: l.statusLabel),
            const Spacer(),
            Text(dateText(l.createdAt), style: const TextStyle(color: Brand.muted, fontSize: 12)),
          ]),
          const SizedBox(height: 10),
          Text(_statusHint(l), style: const TextStyle(color: Brand.ink)),
          if (l.trackingCode != null) ...[
            const SizedBox(height: 12),
            Row(children: [
              const Icon(Icons.local_shipping_outlined, size: 18, color: Brand.navy),
              const SizedBox(width: 6),
              Text('${l.carrier ?? 'PTT'} takip no: ', style: const TextStyle(color: Brand.muted)),
              Flexible(child: SelectableText(l.trackingCode!, style: const TextStyle(fontWeight: FontWeight.w700))),
              IconButton(
                tooltip: 'Kopyala',
                visualDensity: VisualDensity.compact,
                icon: const Icon(Icons.copy, size: 18),
                onPressed: () {
                  Clipboard.setData(ClipboardData(text: l.trackingCode!));
                  showMessage(context, 'Takip numarası kopyalandı.');
                },
              ),
            ]),
            if (l.carrierLastMovement != null)
              Text(
                  'Son hareket: ${l.carrierLastMovement}${l.carrierLastMovementAt == null ? '' : ' · ${dateTimeText(l.carrierLastMovementAt!)}'}',
                  style: const TextStyle(color: Brand.muted, fontSize: 13)),
          ],
        ]),
      ),
      if (l.isAwaitingPayment) ...[
        const SizedBox(height: 12),
        _pendingPayment(l),
      ],
      const SizedBox(height: 12),
      // Mektup önizlemesi
      _LetterPreview(letter: l, settings: settings),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Zarf',
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Alıcı', style: TextStyle(color: Brand.muted, fontSize: 12)),
          Text(l.recipientName, style: const TextStyle(fontWeight: FontWeight.w700)),
          if (l.recipientDetail != null) Text(l.recipientDetail!),
          Text(l.recipientAddress.toString()),
          const SizedBox(height: 10),
          const Text('Gönderen', style: TextStyle(color: Brand.muted, fontSize: 12)),
          Text(l.senderName, style: const TextStyle(fontWeight: FontWeight.w700)),
          Text(l.senderAddress.toString()),
        ]),
      ),
      const SizedBox(height: 12),
      _photos(l, settings),
      if (l.paidAmount != null) ...[
        const SizedBox(height: 12),
        SectionCard(
          title: 'Ödeme',
          child: Column(children: [
            if (l.campaignDiscount > 0)
              InfoRow('🎁 ${l.campaignTitle ?? 'Kampanya'}', '−${money(l.campaignDiscount)}', valueColor: Brand.success),
            if (l.pointsDiscount > 0)
              InfoRow('MektupPuan (${number(l.pointsUsed)} puan)', '−${money(l.pointsDiscount)}', valueColor: Brand.success),
            InfoRow('Ödenen', money(l.paidAmount!), bold: true),
            if (l.paymentMethod != null) InfoRow('Yöntem', l.paymentMethod == 'Mobile' ? 'Mobil ödeme' : 'Kart'),
            if (l.pointsToEarn > 0) InfoRow('Kazanılacak puan', number(l.pointsToEarn)),
          ]),
        ),
      ],
      if (l.canReview || l.review != null) ...[
        const SizedBox(height: 12),
        _ReviewCard(letter: l, onSubmitted: _show),
      ],
      if (l.history.isNotEmpty) ...[
        const SizedBox(height: 12),
        SectionCard(
          title: 'Durum geçmişi',
          child: Timeline(items: [
            for (final h in l.history.reversed) (title: h.statusLabel, note: h.note, at: h.at, status: h.status),
          ]),
        ),
      ],
    ];
  }

  String _statusHint(Letter l) => switch (l.status) {
        'Draft' => l.content.trim().isEmpty
            ? 'Taslak. Mektubunuzu yazıp ödemeye geçebilirsiniz.'
            : 'Taslak hazır. Ödemeyi tamamladığınızda mektubunuz ilk iş gününde basılıp postaya verilir.',
        'AwaitingPayment' => 'Ödemeniz bekleniyor.',
        'Paid' => 'Ödemeniz alındı. Mektubunuz ilk iş gününde basılacak.',
        'InProduction' => 'Mektubunuz basılıyor ve zarflanıyor.',
        'Shipped' => 'Mektubunuz kayıtlı postayla yolda.',
        'Delivered' => 'Mektubunuz teslim edildi. 💌',
        'Cancelled' => 'Bu mektup iptal edildi.',
        _ => '',
      };

  Widget _pendingPayment(Letter l) {
    final p = l.pendingPayment;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFFFF7E6),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFF1D9A6)),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        const Text('Ödeme tamamlanmadı', style: TextStyle(fontWeight: FontWeight.w700, color: Color(0xFF8A5A00))),
        const SizedBox(height: 4),
        Text(
          p == null
              ? 'Ödeme sonucu bekleniyor. Birkaç dakika içinde güncellenecek.'
              : 'Başlattığınız ${money(p.amount)} tutarındaki ödemeye dönebilir veya vazgeçip mektubu düzenleyebilirsiniz.',
          style: const TextStyle(color: Color(0xFF6B4A00)),
        ),
        const SizedBox(height: 12),
        Row(children: [
          if (p != null)
            Expanded(
              child: FilledButton(onPressed: _busy ? null : () => _resumePayment(p), child: const Text('Ödemeye dön')),
            ),
          if (p != null) const SizedBox(width: 10),
          Expanded(child: OutlinedButton(onPressed: _busy ? null : _abandon, child: const Text('Vazgeç'))),
        ]),
      ]),
    );
  }

  Widget _photos(Letter l, AppSettings s) {
    final client = AppScope.of(context).api.client;
    final canAdd = l.isEditable && l.photos.length < s.maxPhotos;
    if (l.photos.isEmpty && !l.isEditable) return const SizedBox.shrink();
    return SectionCard(
      title: 'Fotoğraflar (${l.photos.length}/${s.maxPhotos})',
      trailing: l.isEditable
          ? Text('Adet: +${money(s.prices.photo)}', style: const TextStyle(color: Brand.muted, fontSize: 12))
          : null,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        if (l.photos.isNotEmpty)
          GridView.count(
            crossAxisCount: 3,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            children: [
              for (final p in l.photos)
                Stack(fit: StackFit.expand, children: [
                  ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.network(
                      client.absolute(p.url),
                      headers: client.authHeaders,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) =>
                          Container(color: Brand.paper2, child: const Icon(Icons.broken_image_outlined, color: Brand.muted)),
                    ),
                  ),
                  if (l.isEditable)
                    Positioned(
                      top: 4,
                      right: 4,
                      child: Material(
                        color: Colors.black54,
                        shape: const CircleBorder(),
                        child: InkWell(
                          customBorder: const CircleBorder(),
                          onTap: _busy ? null : () => _removePhoto(p),
                          child: const Padding(
                            padding: EdgeInsets.all(4),
                            child: Icon(Icons.close, size: 16, color: Colors.white, semanticLabel: 'Fotoğrafı kaldır'),
                          ),
                        ),
                      ),
                    ),
                ]),
            ],
          ),
        if (l.photos.isEmpty)
          const Text('Mektubunuzla birlikte basılacak fotoğraflar ekleyebilirsiniz.', style: TextStyle(color: Brand.muted)),
        if (canAdd) ...[
          const SizedBox(height: 12),
          Row(children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: _busy ? null : () => _addPhoto(ImageSource.gallery),
                icon: const Icon(Icons.photo_library_outlined),
                label: const Text('Galeriden'),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: _busy ? null : () => _addPhoto(ImageSource.camera),
                icon: const Icon(Icons.photo_camera_outlined),
                label: const Text('Kamera'),
              ),
            ),
          ]),
        ],
      ]),
    );
  }
}

class _LetterPreview extends StatelessWidget {
  const _LetterPreview({required this.letter, required this.settings});
  final Letter letter;
  final AppSettings settings;

  @override
  Widget build(BuildContext context) {
    final l = letter;
    final style = LetterLook.textStyle(l.font, l.ink);
    final extras = [
      '${settings.label(settings.paperColors, l.paper)} kâğıt',
      '${settings.label(settings.envelopeColors, l.envelope)} zarf',
      settings.label(settings.fonts, l.font),
      if (l.scent != 'None') settings.label(settings.scents, l.scent),
      if (l.postcardCategory != null) '${settings.label(settings.postcardCategories, l.postcardCategory)} kartpostal',
      '${l.pageCount} sayfa',
    ];
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Container(
        constraints: const BoxConstraints(minHeight: 180),
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: LetterLook.paperColor(l.paper),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: Brand.line),
          boxShadow: const [BoxShadow(color: Color(0x14000000), blurRadius: 12, offset: Offset(0, 4))],
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(l.content.isEmpty ? 'Mektup metni henüz yazılmadı.' : l.content,
              style: l.content.isEmpty ? style.copyWith(color: Brand.muted) : style),
          const SizedBox(height: 12),
          Align(alignment: Alignment.centerRight, child: Text('— ${l.senderName}', style: style)),
        ]),
      ),
      const SizedBox(height: 8),
      Wrap(spacing: 6, runSpacing: 6, children: [
        for (final e in extras)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
            decoration: BoxDecoration(color: Brand.paper2, borderRadius: BorderRadius.circular(8)),
            child: Text(e, style: const TextStyle(fontSize: 12, color: Brand.muted)),
          ),
        if (l.postcardCategory != null) PostcardTile(category: l.postcardCategory!, design: l.postcardDesign ?? 1, size: 22),
      ]),
    ]);
  }
}

class _ReviewCard extends StatefulWidget {
  const _ReviewCard({required this.letter, required this.onSubmitted});
  final Letter letter;
  final void Function(Letter) onSubmitted;

  @override
  State<_ReviewCard> createState() => _ReviewCardState();
}

class _ReviewCardState extends State<_ReviewCard> {
  final _comment = TextEditingController();
  int _rating = 0;
  bool _busy = false;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_rating == 0) {
      showMessage(context, 'Lütfen 1-5 arası puan verin.', error: true);
      return;
    }
    if (_comment.text.trim().length < 10) {
      showMessage(context, 'Yorumunuz en az 10 karakter olmalıdır.', error: true);
      return;
    }
    setState(() => _busy = true);
    try {
      final l = await AppScope.read(context).api.review(widget.letter.id, _rating, _comment.text.trim());
      widget.onSubmitted(l);
      if (mounted) showMessage(context, 'Teşekkürler! Yorumunuz onaylandıktan sonra yayınlanacak. 💌');
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final r = widget.letter.review;
    if (r != null) {
      return SectionCard(
        title: 'Yorumunuz',
        trailing: Text(r.statusLabel, style: const TextStyle(color: Brand.muted, fontSize: 12)),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Stars(rating: r.rating.toDouble()),
          if (r.comment.isNotEmpty) ...[const SizedBox(height: 6), Text(r.comment)],
          if (r.rejectionReason != null)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text('Yayınlanmadı: ${r.rejectionReason}', style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ),
        ]),
      );
    }
    return SectionCard(
      title: 'Mektubunuz ulaştı, deneyiminizi puanlayın',
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          for (var i = 1; i <= 5; i++)
            IconButton(
              tooltip: '$i yıldız',
              iconSize: 36,
              onPressed: () => setState(() => _rating = i),
              icon: Icon(_rating >= i ? Icons.star_rounded : Icons.star_outline_rounded, color: const Color(0xFFE0A100)),
            ),
        ]),
        TextField(
          controller: _comment,
          minLines: 2,
          maxLines: 5,
          maxLength: 1000,
          textCapitalization: TextCapitalization.sentences,
          decoration: const InputDecoration(hintText: 'Deneyiminizi birkaç cümleyle anlatın'),
        ),
        const SizedBox(height: 8),
        FilledButton(onPressed: _busy ? null : _submit, child: const Text('Yorumu gönder')),
      ]),
    );
  }
}
