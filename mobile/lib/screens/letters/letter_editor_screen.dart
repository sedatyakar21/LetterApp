import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/address_fields.dart';
import '../../ui/common.dart';
import '../../ui/envelope_preview.dart';
import '../../ui/letter_style.dart';
import '../../ui/photo_viewer.dart';
import '../../ui/theme.dart';
import 'payment_step.dart';

/// Düzenleyiciden dönüş: kaydedilen mektup ve ödemeye geçilecek mi.
/// Kağıt önizlemesi her temada açık renklidir; üstündeki araç çubuğu da açık temadaki renkleri kullanır.
const _paperInk = Color(0xFF232A3D);
const _paperMuted = Color(0xFF686D7A);

class EditorOutcome {
  const EditorOutcome(this.letter, {this.checkout = false});
  final Letter letter;
  final bool checkout;
}

/// Mektup Yaz / Düzenle: Mektup → Ekstralar → İletişim (alıcı + gönderen) → Kontrol → Ödeme (sitedeki sekmelerle aynı).
class LetterEditorScreen extends StatefulWidget {
  const LetterEditorScreen({super.key, this.letter});
  final Letter? letter;

  @override
  State<LetterEditorScreen> createState() => _LetterEditorScreenState();
}

class _LetterEditorScreenState extends State<LetterEditorScreen> {
  // Sitedeki sekmelerle aynı sıra: mektup → ekstralar → iletişim bilgileri (alıcı + gönderen) → kontrol → ödeme.
  static const _steps = ['Mektup', 'Ekstralar', 'İletişim', 'Kontrol', 'Ödeme'];
  static const _contentIndex = 0, _recipientIndex = 2, _senderIndex = 2, _reviewIndex = 3, _paymentIndex = 4;
  final _paymentKey = GlobalKey<PaymentStepState>();

  final _forms = List.generate(5, (_) => GlobalKey<FormState>());
  late final LetterDraft _draft;
  late final TextEditingController _recipientName, _recipientDetail, _fatherName, _ward, _content, _senderName;
  late final AddressControllers _recipientAddress, _senderAddress;
  int _step = 0;
  bool _busy = false;
  /// Kaydedilen taslağın kimliği (yeni mektupta mektup adımındaki "Devam et" ile oluşur).
  late String? _letterId = widget.letter?.id;
  late List<Photo> _photos = widget.letter?.photos ?? const [];
  late List<LetterDocument> _documents = widget.letter?.documents ?? const [];
  bool _attachBusy = false;
  bool _dirty = false;
  // Önceki mektuptan doldurma: alıcı adı hâlâ otomatik gelen adsa tür değişince yeniden doldurulur.
  String? _autoName;
  int _addressVersion = 0; // adres alanları dışarıdan değişince yeniden kurulsun

  @override
  void initState() {
    super.initState();
    final l = widget.letter;
    _draft = l == null ? LetterDraft(senderName: AppScope.read(context).me?.fullName ?? '') : LetterDraft.fromLetter(l);
    _recipientName = TextEditingController(text: _draft.recipientName);
    _recipientDetail = TextEditingController(text: _draft.recipientDetail ?? '');
    _fatherName = TextEditingController(text: _draft.recipientFatherName ?? '');
    _ward = TextEditingController(text: _draft.recipientWard ?? '');
    _content = TextEditingController(text: _draft.content);
    _senderName = TextEditingController(text: _draft.senderName);
    _recipientAddress = AddressControllers(_draft.recipientAddress);
    _senderAddress = AddressControllers(_draft.senderAddress);
    for (final c in [_recipientName, _recipientDetail, _fatherName, _ward, _content, _senderName]) {
      c.addListener(_markDirty);
    }
    _recipientAddress.addListener(_markDirty);
    _senderAddress.addListener(_markDirty);
    _content.addListener(() => setState(() {})); // sayfa sayısı ve önizleme
    if (l == null) WidgetsBinding.instance.addPostFrameCallback((_) => _applyPrefill());
  }

  /// Yeni mektupta cezaevi / asker seçiliyken o türdeki son mektubun alıcı ve gönderen bilgilerini yazar.
  /// Kullanıcı alıcı adını kendisi değiştirdiyse dokunulmaz.
  Future<void> _applyPrefill() async {
    if (widget.letter != null) return;
    final name = _recipientName.text.trim();
    if (name.isNotEmpty && name != _autoName) return;
    final type = _draft.recipientType;
    LetterDraft? p;
    if (type != 'Standard') {
      try {
        p = await AppScope.read(context).api.prefill(type);
      } catch (_) {
        return; // bağlantı hatasında form boş kalır, kullanıcı elle doldurur
      }
    }
    if (!mounted || _draft.recipientType != type) return;
    final wasDirty = _dirty;
    setState(() {
      _recipientName.text = p?.recipientName ?? '';
      _recipientDetail.text = p?.recipientDetail ?? '';
      _fatherName.text = p?.recipientFatherName ?? '';
      _ward.text = p?.recipientWard ?? '';
      _setAddress(_recipientAddress, p?.recipientAddress ?? const Address());
      if (p != null) {
        _senderName.text = p.senderName;
        _setAddress(_senderAddress, p.senderAddress);
      }
      _autoName = p?.recipientName;
      _addressVersion++;
    });
    _dirty = wasDirty; // otomatik doldurma "kaydedilmemiş değişiklik" sayılmaz
  }

  static void _setAddress(AddressControllers c, Address a) {
    c.line.text = a.line;
    c.district.text = a.district;
    c.city.text = a.city;
    c.postalCode.text = a.postalCode;
  }

  @override
  void dispose() {
    for (final c in [_recipientName, _recipientDetail, _fatherName, _ward, _content, _senderName]) {
      c.dispose();
    }
    _recipientAddress.dispose();
    _senderAddress.dispose();
    super.dispose();
  }

  void _markDirty() => _dirty = true;

  void _change(VoidCallback f) => setState(() {
        f();
        _dirty = true;
      });

  LetterDraft _collect() => _draft
    ..recipientName = _recipientName.text.trim()
    ..recipientDetail = _recipientDetail.text.trim().isEmpty ? null : _recipientDetail.text.trim()
    ..recipientFatherName = _fatherName.text.trim().isEmpty ? null : _fatherName.text.trim()
    ..recipientWard = _ward.text.trim().isEmpty ? null : _ward.text.trim()
    ..recipientAddress = _recipientAddress.value
    ..content = _content.text
    ..senderName = _senderName.text.trim()
    ..senderAddress = _senderAddress.value;

  bool _validateUpTo(int last) {
    for (var i = 0; i <= last; i++) {
      final state = _forms[i].currentState;
      if (state != null && !state.validate()) {
        setState(() => _step = i);
        return false;
      }
    }
    return true;
  }

  /// Adımdan çıkmadan önce kontrol: mektup adımında metin boş olamaz, diğerlerinde form doğrulaması.
  bool _canLeave(int step) {
    if (step == _contentIndex && _content.text.trim().isEmpty) {
      showMessage(context, 'Devam etmek için önce mektubunuzu yazın.', error: true);
      return false;
    }
    return _forms[step].currentState?.validate() ?? true;
  }

  Future<void> _next() => _goTo(_step + 1);

  /// Sekmeye dokununca: geri her zaman; ileri, aradaki adımlar tamamsa.
  /// Mektup adımından ileri geçilirken taslak kaydedilir (kayıt başarısız olsa da devam edilir, uyarı gösterilir).
  Future<void> _goTo(int target) async {
    // Ödeme adımından geri çıkarken açık ödeme denemesi kapatılır (sorulur; mektup yeniden düzenlenebilir olur).
    if (_step == _paymentIndex && target < _paymentIndex) {
      if (!(await _paymentKey.currentState?.leave() ?? true)) return;
    }
    if (target <= _step) {
      setState(() => _step = target);
      return;
    }
    final leavingLetter = _step == _contentIndex;
    for (var i = _step; i < target; i++) {
      if (!_canLeave(i)) {
        setState(() => _step = i);
        return;
      }
    }
    // Ödemeden önce son hal kaydedilir (ödeme sunucudaki kayda göre hesaplanır).
    if (leavingLetter || target == _paymentIndex) {
      final saved = await _autosave(quiet: target == _paymentIndex);
      if (target == _paymentIndex && (!saved || _letterId == null)) return;
    }
    if (mounted) setState(() => _step = target);
  }

  /// Ödeme tamamlandı: teşekkür penceresi, ardından mektup ekranı.
  Future<void> _onPaid(bool free) => _finishOrder(
        title: 'Siparişiniz alındı!',
        message: free
            ? 'Ödemeniz MektupPuan / kampanya indirimiyle tamamlandı. Mektubunuz ilk iş gününde basılıp postaya verilecek.'
            : 'Ödemeniz alındı. Mektubunuz ilk iş gününde basılıp postaya verilecek.',
      );

  /// Havale / EFT ile sipariş oluşturuldu: mektup ekranında hesap bilgileri ve sipariş no gösterilir.
  Future<void> _onBankTransferOrdered() => _finishOrder(
        title: 'Siparişiniz oluşturuldu',
        message: 'Havale / EFT hesabımıza geçtiğinde ödemeniz onaylanır ve mektubunuz basıma alınır. '
            'Hesap bilgileri ve açıklamaya yazacağınız sipariş numarası mektup ekranında.',
        icon: Icons.account_balance_outlined,
        button: 'Hesap bilgilerini göster',
      );

  Future<void> _finishOrder({required String title, required String message,
      IconData icon = Icons.mark_email_read_outlined, String button = 'Harika'}) async {
    final session = AppScope.read(context);
    session.lettersChanged();
    session.refreshMe().catchError((_) {});
    _dirty = false;
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (c) => AlertDialog(
        icon: Icon(icon, size: 48, color: Brand.success),
        title: Text(title),
        content: Text(message),
        actions: [FilledButton(onPressed: () => Navigator.pop(c), child: Text(button))],
      ),
    );
    if (!mounted) return;
    try {
      final letter = await session.api.letter(_letterId!);
      if (mounted) Navigator.of(context).pop(EditorOutcome(letter));
    } catch (_) {
      if (mounted) Navigator.of(context).pop();
    }
  }

  /// Mektup adımındaki "Devam et": taslağı ekrandan çıkmadan kaydeder (alıcı / gönderen henüz boş olabilir).
  /// Yeni mektupta dönen kimlik saklanır; sonraki kayıtlar aynı taslağı günceller.
  Future<bool> _autosave({bool quiet = false}) async {
    if (_busy) return false;
    setState(() => _busy = true);
    final session = AppScope.read(context);
    try {
      final saved = await session.api.saveLetter(_letterId, _collect());
      _letterId = saved.id;
      _dirty = false;
      session.lettersChanged();
      if (mounted && !quiet) showMessage(context, 'Taslak kaydedildi.');
      return true;
    } catch (e) {
      if (mounted) showMessage(context, 'Taslak kaydedilemedi: ${errorText(e)}', error: true);
      return false;
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// [attachments]: "Fotoğraf / belge ekle" düğmesinden; kaydedip mektup ekranına (yükleme bölümlerine) döner.
  Future<void> _save({required bool checkout, bool attachments = false}) async {
    // Taslak, alıcı / gönderen bilgileri eksikken de kaydedilebilir; ödemeye geçerken hepsi zorunludur.
    final draft = _collect();
    if (checkout) {
      if (!_validateUpTo(_steps.length - 1)) return;
      if (draft.recipientName.isEmpty || draft.recipientAddress.line.isEmpty) {
        setState(() => _step = _recipientIndex);
        return;
      }
      if (draft.senderName.isEmpty || draft.senderAddress.line.isEmpty) {
        setState(() => _step = _senderIndex);
        showMessage(context, 'Lütfen gönderen bilgilerini doldurun.', error: true);
        return;
      }
    }
    if (checkout && draft.content.trim().isEmpty) {
      setState(() => _step = _contentIndex);
      showMessage(context, 'Ödemeye geçmeden önce mektubunuzu yazın.', error: true);
      return;
    }
    setState(() => _busy = true);
    final session = AppScope.read(context);
    try {
      final saved = await session.api.saveLetter(_letterId, draft);
      _letterId = saved.id;
      session.lettersChanged();
      _dirty = false;
      if (!mounted) return;
      if (!checkout) {
        showMessage(context,
            attachments ? 'Taslak kaydedildi. Fotoğraf ve belgeleri mektup ekranından ekleyebilirsiniz.' : 'Taslak kaydedildi.');
      }
      Navigator.of(context).pop(EditorOutcome(saved, checkout: checkout));
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  /// Kaydedilmemiş değişiklikle çıkarken: "Yaptığınız değişiklikler kaydedilsin mi?" (Kaydet / Kaydetme / Vazgeç).
  /// Kaydet seçilirse taslak kaydedilip ekran kapanır (_save kendisi kapatır); false: ekranda kal.
  Future<bool> _confirmLeave() async {
    if (!_dirty) return true;
    final choice = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Değişiklikleriniz kaydedilmedi'),
        content: const Text('Yaptığınız değişiklikler kaydedilsin mi? Kaydederseniz mektubunuz taslak olarak saklanır.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(ctx).pop('cancel'), child: const Text('Vazgeç')),
          TextButton(
            onPressed: () => Navigator.of(ctx).pop('discard'),
            style: TextButton.styleFrom(foregroundColor: Theme.of(ctx).colorScheme.error),
            child: const Text('Kaydetme'),
          ),
          FilledButton(onPressed: () => Navigator.of(ctx).pop('save'), child: const Text('Kaydet')),
        ],
      ),
    );
    if (choice == 'save') {
      await _save(checkout: false);
      return false;
    }
    return choice == 'discard';
  }

  @override
  Widget build(BuildContext context) {
    final settings = AppScope.of(context).settings!;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        if (_step > 0) {
          await _goTo(_step - 1);
          return;
        }
        if (await _confirmLeave() && context.mounted) Navigator.of(context).pop();
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.letter == null ? 'Mektup Yaz' : 'Mektubu Düzenle'),
          // Mektup adımında "Taslak kaydet" yok (taslak "Devam et" ile kaydedilir); Kontrol adımında da gereksiz.
          actions: [
            if (_step != _contentIndex && _step != _reviewIndex)
              TextButton(onPressed: _busy ? null : () => _save(checkout: false), child: const Text('Taslak kaydet')),
          ],
          bottom: PreferredSize(
              preferredSize: const Size.fromHeight(56), child: _StepBar(steps: _steps, current: _step, onTap: _goTo)),
        ),
        body: SafeArea(
          child: IndexedStack(index: _step, children: [
            _page(0, _contentStep(settings)),
            _page(1, _styleStep(settings)),
            _page(2, Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              _recipientStep(settings),
              const SizedBox(height: 16),
              _senderStep(),
            ])),
            _page(3, _reviewStep(settings)),
            // Ödeme adımı yalnızca açıkken kurulur (kurulunca ödeme başlatılır).
            _page(4, _step == _paymentIndex && _letterId != null
                ? PaymentStep(
                    key: _paymentKey,
                    letterId: _letterId!,
                    onPaid: _onPaid,
                    onBankTransferOrdered: _onBankTransferOrdered)
                : const SizedBox.shrink()),
          ]),
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
            child: Row(children: [
              if (_step > 0)
                Expanded(
                  child: OutlinedButton(onPressed: _busy ? null : () => _goTo(_step - 1), child: const Text('Geri')),
                ),
              if (_step > 0) const SizedBox(width: 12),
              Expanded(
                flex: 2,
                // Ödeme adımında ödeme, adımın içindeki kart formuyla yapılır (altta yalnızca "Geri").
                child: _step < _steps.length - 1
                    ? FilledButton(
                        onPressed: _busy ? null : _next,
                        child: _busy && (_step == _contentIndex || _step == _reviewIndex)
                            ? const SizedBox(
                                width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                            : Text('Devam et: ${_steps[_step + 1]}'))
                    : const SizedBox.shrink(),
              ),
            ]),
          ),
        ),
      ),
    );
  }

  Widget _page(int i, Widget child) => Form(
        key: _forms[i],
        child: ListView(padding: const EdgeInsets.all(16), children: [child]),
      );

  // ---------------- 3. İletişim: alıcı ----------------

  Widget _recipientStep(AppSettings s) {
    final type = _draft.recipientType;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionCard(
        title: 'Mektup kime gidiyor?',
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          SegmentedButton<String>(
            showSelectedIcon: false,
            segments: [
              for (final o in s.recipientTypes)
                ButtonSegment(value: o.value, label: Text(o.value == 'Standard' ? 'Sevdiklerim' : o.label)),
            ],
            selected: {type},
            onSelectionChanged: (v) {
              _change(() => _draft.recipientType = v.first);
              _applyPrefill();
            },
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _recipientName,
            maxLength: 100,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Alıcı ad soyad', counterText: ''),
            validator: (v) => (v ?? '').trim().isEmpty ? 'Alıcı adı zorunludur.' : null,
          ),
          if (type == 'Soldier') ...[
            const SizedBox(height: 12),
            TextFormField(
              controller: _recipientDetail,
              maxLength: 200,
              decoration: const InputDecoration(labelText: 'Birlik bilgisi (rütbe, birlik adı)', counterText: ''),
              validator: (v) => (v ?? '').trim().isEmpty ? 'Askere giden mektuplarda birlik bilgisi zorunludur.' : null,
            ),
          ],
          // Cezaevi: baba adı ve koğuş; kurum adı seçilen cezaevinden gelir (ayrı alan gösterilmez).
          if (type == 'Inmate') ...[
            const SizedBox(height: 12),
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Expanded(
                flex: 3,
                child: TextFormField(
                  controller: _fatherName,
                  maxLength: 60,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(labelText: 'Alıcının baba adı', counterText: ''),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                flex: 2,
                child: TextFormField(
                  controller: _ward,
                  maxLength: 30,
                  textCapitalization: TextCapitalization.characters,
                  decoration: const InputDecoration(labelText: 'Koğuş no', hintText: 'Ör. B-12', counterText: ''),
                ),
              ),
            ]),
            const SizedBox(height: 6),
            // İsteğe bağlı ama tavsiye edilir; boş bırakılırsa kaydı engellemez.
            Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Icon(Icons.lightbulb_outline, size: 16, color: Theme.of(context).colorScheme.primary),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  'Zorunlu değil ama yazmanızı tavsiye ederiz: baba adı ve koğuş numarası, mektubun aynı adlı '
                  'kişilerle karışmadan doğrudan alıcıya ulaşmasını kolaylaştırır.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            ]),
          ],
        ]),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: type == 'Inmate' ? 'Cezaevi adresi' : 'Alıcı adresi',
        child: AddressFields(
          key: ValueKey('recipient-address-$_addressVersion'),
          controllers: _recipientAddress,
          prisonMode: type == 'Inmate',
          // Kurum adı gizli ek bilgi alanında saklanır (zarfa ve PTT kaydına basılır).
          onPrisonPicked: (p) {
            _recipientDetail.text = p.name;
            _markDirty();
          },
        ),
      ),
    ]);
  }

  // ---------------- 1. Mektup ----------------

  Widget _contentStep(AppSettings s) {
    final pages = s.pageCount(_content.text);
    final p = s.prices;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      // Kağıt ve zarf rengi mektubun hemen üstünde (sitedeki formla aynı): kâğıdın rengi yazarken görülür.
      SectionCard(
        title: 'Kâğıt rengi',
        trailing: Text('Renkli: +${money(p.coloredPaper)}', style: TextStyle(color: Brand.muted, fontSize: 12)),
        child: _chips(s.paperColors, _draft.paper, (v) => _draft.paper = v,
            avatar: (v) => Swatch(LetterLook.paperColor(v))),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Zarf rengi',
        trailing: Text('Renkli: +${money(p.coloredEnvelope)}', style: TextStyle(color: Brand.muted, fontSize: 12)),
        child: _chips(s.envelopeColors, _draft.envelope, (v) => _draft.envelope = v,
            avatar: (v) => Swatch(LetterLook.envelope[v] ?? Colors.white)),
      ),
      const SizedBox(height: 16),
      Container(
        decoration: BoxDecoration(
          color: LetterLook.paperColor(_draft.paper),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: Brand.line),
          boxShadow: const [BoxShadow(color: Color(0x14000000), blurRadius: 12, offset: Offset(0, 4))],
        ),
        clipBehavior: Clip.antiAlias,
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          // Editörün araç çubuğu: yazı stili ve mürekkep (sitedeki editörle aynı); metin seçime göre hemen değişir.
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: const Color(0xCCF7F6F0),
              border: Border(bottom: BorderSide(color: Brand.line)),
            ),
            child: Wrap(spacing: 12, crossAxisAlignment: WrapCrossAlignment.center, children: [
              _toolbarMenu(
                tooltip: 'Yazı stili',
                icon: const Icon(Icons.text_fields, size: 18, color: _paperMuted),
                options: s.fonts,
                value: _draft.font,
                onSelect: (v) => _draft.font = v,
              ),
              _toolbarMenu(
                tooltip: 'Mürekkep rengi',
                icon: Swatch(LetterLook.inkColor(_draft.ink), size: 16),
                options: s.inks,
                value: _draft.ink,
                onSelect: (v) => _draft.ink = v,
                itemIcon: (v) => Swatch(LetterLook.inkColor(v), size: 14),
              ),
              _toolbarMenu(
                tooltip: 'Yazı boyutu',
                icon: const Icon(Icons.format_size, size: 18, color: _paperMuted),
                options: s.textSizes,
                value: _draft.textSize,
                onSelect: (v) => _draft.textSize = v,
              ),
            ]),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 14, 18, 8),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          TextFormField(
            controller: _content,
            minLines: 14,
            maxLines: null,
            maxLength: s.maxContentLength,
            keyboardType: TextInputType.multiline,
            textCapitalization: TextCapitalization.sentences,
            style: LetterLook.textStyle(_draft.font, _draft.ink, _draft.textSize),
            decoration: const InputDecoration(
              hintStyle: TextStyle(color: Color(0x99686D7A)),
              hintText: 'Sevgili ...\n\nBuraya yazdıklarınız kâğıda basılıp zarflanacak.',
              filled: false,
              border: InputBorder.none,
              enabledBorder: InputBorder.none,
              focusedBorder: InputBorder.none,
              counterText: '',
              contentPadding: EdgeInsets.zero,
            ),
          ),
            ]),
          ),
        ]),
      ),
      const SizedBox(height: 10),
      Row(children: [
        Icon(Icons.description_outlined, size: 16, color: Brand.muted.withValues(alpha: 0.9)),
        const SizedBox(width: 6),
        Text('$pages sayfa', style: TextStyle(color: Brand.muted)),
        if (pages > 1)
          Text(' (+${money((pages - 1) * s.prices.extraPage)})', style: TextStyle(color: Brand.muted)),
        const Spacer(),
        Text('${_content.text.length} / ${s.maxContentLength}', style: TextStyle(color: Brand.muted, fontSize: 12)),
      ]),
      const SizedBox(height: 4),
      Text('İlk sayfa fiyata dahildir; her ek sayfa ${money(s.prices.extraPage)}.',
          style: TextStyle(color: Brand.muted, fontSize: 12)),
    ]);
  }

  /// Editör araç çubuğundaki açılır seçim (seçili değerin adı + simge; dokununca liste açılır).
  Widget _toolbarMenu({
    required String tooltip,
    required Widget icon,
    required List<Option> options,
    required String value,
    required void Function(String) onSelect,
    Widget Function(String)? itemIcon,
  }) {
    final label = options.firstWhere((o) => o.value == value, orElse: () => Option(value, value)).label;
    return PopupMenuButton<String>(
      tooltip: tooltip,
      initialValue: value,
      onSelected: (v) => _change(() => onSelect(v)),
      itemBuilder: (_) => [
        for (final o in options)
          PopupMenuItem(
            value: o.value,
            child: Row(children: [
              if (itemIcon != null) ...[itemIcon(o.value), const SizedBox(width: 8)],
              Text(o.label),
              if (o.value == value) ...[const Spacer(), const Icon(Icons.check, size: 18)],
            ]),
          ),
      ],
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          icon,
          const SizedBox(width: 6),
          // Araç çubuğu kağıdın üstünde: koyu modda da açık zemin, koyu yazı.
          Text(label, style: const TextStyle(fontWeight: FontWeight.w600, color: _paperInk)),
          const Icon(Icons.arrow_drop_down, color: _paperMuted),
        ]),
      ),
    );
  }

  // ---------------- 2. Ekstralar ----------------

  Widget _styleStep(AppSettings s) {
    final p = s.prices;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionCard(
        title: 'Koku',
        trailing: Text('+${money(p.scent)}', style: TextStyle(color: Brand.muted, fontSize: 12)),
        child: Wrap(spacing: 8, runSpacing: 8, children: [
          ChoiceChip(
            label: const Text('Kokusuz'),
            selected: _draft.scentId == null,
            onSelected: (_) => _change(() => _draft.scentId = null),
          ),
          for (final sc in _scentOptions(s))
            ChoiceChip(
              avatar: const Text('🌸'),
              label: Text(sc.name),
              selected: _draft.scentId == sc.id,
              onSelected: (_) => _change(() => _draft.scentId = sc.id),
            ),
        ]),
      ),
      const SizedBox(height: 12),
      if (_postcardOptions(s).isNotEmpty) ...[
        SectionCard(
          title: 'Kartpostal',
          trailing: Text('her biri +${money(p.postcard)}', style: TextStyle(color: Brand.muted, fontSize: 12)),
          child: _postcardPicker(s),
        ),
      ],
      const SizedBox(height: 12),
      _attachmentsCard(s),
    ]);
  }

  // ---------------- Fotoğraf ve belge (Ekstralar adımında, ekrandan çıkmadan) ----------------

  /// Yükleme için mektubun kaydedilmiş olması gerekir (yeni mektupta mektup adımındaki "Devam et" bunu yapar).
  Future<String?> _ensureLetterId() async {
    if (_letterId != null) return _letterId;
    await _autosave();
    return _letterId;
  }

  /// Sunucunun döndürdüğü güncel mektuptan fotoğraf / belge listesini alır.
  void _takeAttachments(Letter l) => setState(() {
        _photos = l.photos;
        _documents = l.documents;
      });

  Future<void> _runAttachment(Future<void> Function(String id) action) async {
    final id = await _ensureLetterId();
    if (id == null || !mounted) return;
    setState(() => _attachBusy = true);
    try {
      await action(id);
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _attachBusy = false);
    }
  }

  Future<void> _addPhoto(ImageSource source) async {
    final settings = AppScope.read(context).settings!;
    if (_photos.length >= settings.maxPhotos) {
      showMessage(context, 'En fazla ${settings.maxPhotos} fotoğraf eklenebilir.', error: true);
      return;
    }
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
    await _runAttachment((id) async {
      _takeAttachments(await AppScope.read(context).api.addPhoto(id, bytes, name));
      if (mounted) showMessage(context, 'Fotoğraf eklendi.');
    });
  }

  Future<void> _removePhoto(Photo p) async {
    if (!await confirm(context, title: 'Fotoğraf kaldırılsın mı?', message: p.fileName, ok: 'Kaldır', danger: true)) return;
    if (!mounted) return;
    await _runAttachment((id) async => _takeAttachments(await AppScope.read(context).api.removePhoto(id, p.id)));
  }

  static const _documentExtensions = ['pdf', 'jpg', 'jpeg', 'png'];

  /// Dosyalardan PDF / JPG / PNG belge seçer.
  Future<void> _pickDocumentFile() async {
    if (!_canAddDocument()) return;
    final PlatformFile? file;
    try {
      file = await FilePicker.pickFile(type: FileType.custom, allowedExtensions: _documentExtensions);
    } catch (_) {
      if (mounted) showMessage(context, 'Dosya seçilemedi.', error: true);
      return;
    }
    if (file == null || !mounted) return;
    if (!_documentExtensions.contains((file.extension ?? '').toLowerCase())) {
      showMessage(context, 'Sadece PDF, JPG veya PNG belge yükleyebilirsiniz.', error: true);
      return;
    }
    await _uploadDocument(await file.readAsBytes(), file.name);
  }

  /// Kâğıt belgeyi kamerayla çeker (JPG olarak yüklenir).
  Future<void> _scanDocument() async {
    if (!_canAddDocument()) return;
    final photo = await ImagePicker().pickImage(source: ImageSource.camera, maxWidth: 2480, maxHeight: 3508, imageQuality: 90);
    if (photo == null || !mounted) return;
    var name = photo.name;
    if (!RegExp(r'\.(jpe?g|png)$', caseSensitive: false).hasMatch(name)) name = '$name.jpg';
    await _uploadDocument(await photo.readAsBytes(), name);
  }

  bool _canAddDocument() {
    final settings = AppScope.read(context).settings!;
    if (_documents.length < settings.maxDocuments) return true;
    showMessage(context, 'En fazla ${settings.maxDocuments} belge eklenebilir.', error: true);
    return false;
  }

  Future<void> _uploadDocument(List<int> bytes, String name) async {
    if (!mounted) return;
    final settings = AppScope.read(context).settings!;
    if (bytes.length > settings.maxDocumentBytes) {
      showMessage(context, 'Belge boyutu en fazla ${settings.maxDocumentBytes ~/ (1024 * 1024)} MB olabilir.', error: true);
      return;
    }
    await _runAttachment((id) async {
      _takeAttachments(await AppScope.read(context).api.addDocument(id, bytes, name));
      if (mounted) showMessage(context, 'Belge eklendi. Mektubunuzla birlikte basılıp zarfa konacak.');
    });
  }

  Future<void> _removeDocument(LetterDocument d) async {
    if (!await confirm(context, title: 'Belge kaldırılsın mı?', message: d.fileName, ok: 'Kaldır', danger: true)) return;
    if (!mounted) return;
    await _runAttachment((id) async => _takeAttachments(await AppScope.read(context).api.removeDocument(id, d.id)));
  }

  /// Fotoğraf ve belge: bu adımda eklenir / kaldırılır (taslak gerekirse önce kaydedilir).
  Widget _attachmentsCard(AppSettings s) {
    final p = s.prices;
    final client = AppScope.read(context).api.client;
    final busy = _attachBusy || _busy;
    return SectionCard(
      title: 'Fotoğraf ve belge',
      trailing: busy ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)) : null,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text('📷 Fotoğraflar · her biri +${money(p.photo)} · en fazla ${s.maxPhotos}',
            style: TextStyle(color: Brand.muted, fontSize: 13)),
        const SizedBox(height: 8),
        Wrap(spacing: 8, runSpacing: 8, children: [
          for (final (i, ph) in _photos.indexed)
            Stack(children: [
              // Dokununca tam ekran açılır (kaydırarak diğer fotoğraflar, iki parmakla yakınlaştırma).
              GestureDetector(
                onTap: () => showPhotoViewer(context,
                    urls: [for (final x in _photos) client.absolute(x.url)],
                    titles: [for (final x in _photos) x.fileName],
                    initialIndex: i,
                    headers: client.authHeaders),
                child: Semantics(
                  button: true,
                  label: '${ph.fileName} fotoğrafını büyüt',
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(8),
                    child: Image.network(client.absolute(ph.url),
                        headers: client.authHeaders,
                        width: 76,
                        height: 76,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => Container(
                            width: 76, height: 76, color: Brand.paper2, child: const Icon(Icons.broken_image_outlined))),
                  ),
                ),
              ),
              Positioned(
                top: 2,
                right: 2,
                child: InkWell(
                  onTap: busy ? null : () => _removePhoto(ph),
                  child: const CircleAvatar(
                      radius: 11, backgroundColor: Color(0x99000000), child: Icon(Icons.close, size: 14, color: Colors.white)),
                ),
              ),
            ]),
        ]),
        const SizedBox(height: 8),
        Row(children: [
          Expanded(
            child: OutlinedButton.icon(
              onPressed: busy || _photos.length >= s.maxPhotos ? null : () => _addPhoto(ImageSource.gallery),
              icon: const Icon(Icons.photo_library_outlined, size: 18),
              label: const Text('Galeriden'),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: busy || _photos.length >= s.maxPhotos ? null : () => _addPhoto(ImageSource.camera),
              icon: const Icon(Icons.photo_camera_outlined, size: 18),
              label: const Text('Fotoğraf çek'),
            ),
          ),
        ]),
        const Divider(height: 24),
        Text('📄 Belgeler · her biri +${money(p.document)} · en fazla ${s.maxDocuments} · PDF, JPG, PNG',
            style: TextStyle(color: Brand.muted, fontSize: 13)),
        for (final d in _documents)
          ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            leading: Icon(d.isPdf ? Icons.picture_as_pdf_outlined : Icons.image_outlined),
            title: Text(d.fileName, maxLines: 1, overflow: TextOverflow.ellipsis),
            trailing: IconButton(
              tooltip: 'Kaldır',
              icon: const Icon(Icons.delete_outline),
              onPressed: busy ? null : () => _removeDocument(d),
            ),
          ),
        const SizedBox(height: 8),
        Row(children: [
          Expanded(
            child: OutlinedButton.icon(
              onPressed: busy || _documents.length >= s.maxDocuments ? null : _pickDocumentFile,
              icon: const Icon(Icons.attach_file, size: 18),
              label: const Text('Dosya seç'),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: busy || _documents.length >= s.maxDocuments ? null : _scanDocument,
              icon: const Icon(Icons.document_scanner_outlined, size: 18),
              label: const Text('Fotoğrafını çek'),
            ),
          ),
        ]),
      ]),
    );
  }
  /// Yönetim panelindeki aktif kokular; düzenlenen mektuptaki koku sonradan pasife alındıysa o da listede kalır.
  List<ScentInfo> _scentOptions(AppSettings s) {
    final current = widget.letter?.scent;
    return [
      ...s.scents,
      if (current?.id != null && s.scents.every((x) => x.id != current!.id)) current!,
    ];
  }

  String _scentName(AppSettings s, String? id) {
    if (id == null) return 'Kokusuz';
    for (final x in _scentOptions(s)) {
      if (x.id == id) return x.name;
    }
    return '—';
  }

  /// Aktif kartpostallar; düzenlenen mektuptaki kartpostallar sonradan pasife alındıysa onlar da listede kalır (seçim kaybolmasın).
  List<PostcardInfo> _postcardOptions(AppSettings s) {
    final list = [...s.postcards];
    for (final current in widget.letter?.postcards ?? const <PostcardInfo>[]) {
      if (current.id != null && list.every((p) => p.id != current.id)) list.add(current);
    }
    return list;
  }

  List<PostcardInfo> _selectedPostcards(AppSettings s) {
    final options = _postcardOptions(s);
    return [
      for (final id in _draft.postcardIds)
        for (final p in options)
          if (p.id == id) p
    ];
  }

  /// Dokununca seçilir / seçim kalkar; sınıra ulaşınca yeni seçim yapılamaz.
  void _togglePostcard(AppSettings s, String id) {
    final ids = _draft.postcardIds;
    if (ids.contains(id)) {
      _change(() => ids.remove(id));
    } else if (ids.length >= s.maxPostcards) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text('En fazla ${s.maxPostcards} kartpostal seçebilirsiniz.')));
    } else {
      _change(() => ids.add(id));
    }
  }

  Widget _postcardPicker(AppSettings s) {
    final client = AppScope.read(context).api.client;
    final groups = <String, List<PostcardInfo>>{};
    for (final p in _postcardOptions(s)) {
      groups.putIfAbsent(p.category, () => []).add(p);
    }
    final count = _draft.postcardIds.length;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Text(
        count == 0
            ? 'Birden fazla seçebilirsiniz (en fazla ${s.maxPostcards}). İstemiyorsanız hiçbirini seçmeyin.'
            : '$count kartpostal seçildi · ${money(s.prices.postcard * count)}',
        style: TextStyle(color: count == 0 ? Brand.muted : Brand.navy, fontSize: 13, fontWeight: count == 0 ? null : FontWeight.w600),
      ),
      if (count > 0)
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            onPressed: () => _change(() => _draft.postcardIds.clear()),
            icon: const Icon(Icons.clear, size: 18),
            label: const Text('Seçimi temizle'),
          ),
        ),
      for (final g in groups.entries) ...[
        const SizedBox(height: 10),
        Text(g.key, style: TextStyle(color: Brand.muted, fontSize: 13)),
        const SizedBox(height: 6),
        Wrap(spacing: 10, runSpacing: 10, children: [
          for (final p in g.value)
            Semantics(
              button: true,
              selected: _draft.postcardIds.contains(p.id),
              label: '${p.title} kartpostalı',
              child: GestureDetector(
                onTap: () => _togglePostcard(s, p.id!),
                child: SizedBox(
                  width: 104,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Stack(children: [
                      PostcardTile(
                          imageUrl: p.imageUrl == null ? null : client.absolute(p.imageUrl!),
                          selected: _draft.postcardIds.contains(p.id),
                          width: 104),
                      if (_draft.postcardIds.contains(p.id))
                        Positioned(
                          top: 4,
                          right: 4,
                          child: CircleAvatar(
                              radius: 10, backgroundColor: Brand.burgundy, child: Icon(Icons.check, size: 14, color: Colors.white)),
                        ),
                    ]),
                    const SizedBox(height: 4),
                    Text(p.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12)),
                  ]),
                ),
              ),
            ),
        ]),
      ],
    ]);
  }

  Widget _chips(List<Option> options, String selected, void Function(String) onSelect, {Widget Function(String)? avatar}) {
    return Wrap(spacing: 8, runSpacing: 8, children: [
      for (final o in options)
        ChoiceChip(
          avatar: avatar?.call(o.value),
          label: Text(o.label),
          selected: selected == o.value,
          showCheckmark: avatar == null,
          onSelected: (_) => _change(() => onSelect(o.value)),
        ),
    ]);
  }

  // ---------------- 3. İletişim: gönderen ----------------

  Widget _senderStep() {
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionCard(
        title: 'Gönderen',
        child: Column(children: [
          TextFormField(
            controller: _senderName,
            maxLength: 100,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Gönderen ad soyad', counterText: ''),
            validator: (v) => (v ?? '').trim().isEmpty ? 'Gönderen adı zorunludur.' : null,
          ),
          const SizedBox(height: 12),
          AddressFields(key: ValueKey('sender-address-$_addressVersion'), controllers: _senderAddress),
        ]),
      ),
      const SizedBox(height: 10),
      Text('Alıcı cevap yazmak isterse zarfın üzerindeki gönderen adresini kullanır.',
          style: TextStyle(color: Brand.muted, fontSize: 13)),
    ]);
  }

  // ---------------- 4. Kontrol: zarf, mektup ve ekstralar ----------------

  Widget _reviewStep(AppSettings s) {
    final d = _collect();
    final client = AppScope.read(context).api.client;
    // Zarftaki "ilçe / il posta kodu" satırı (yönetim panelindeki zarf çıktısıyla aynı).
    String place(Address a) =>
        [[a.district, a.city].where((x) => x.isNotEmpty).join(' / '), a.postalCode].where((x) => x.isNotEmpty).join(' ');
    Widget label(String text, int step) => Row(children: [
          Text(text.toUpperCase(),
              style: TextStyle(color: Brand.muted, fontSize: 12, fontWeight: FontWeight.w700, letterSpacing: .5)),
          const Spacer(),
          TextButton(onPressed: () => setState(() => _step = step), child: const Text('Düzenle')),
        ]);
    final postcards = _selectedPostcards(s);
    final style = LetterLook.textStyle(d.font, d.ink, d.textSize);

    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Text('Göndermeden önce zarfınızı, mektubunuzu ve eklediklerinizi kontrol edin.',
          style: TextStyle(color: Brand.muted)),
      const SizedBox(height: 8),
      label('Zarf', 0), // zarf rengi mektup adımında seçilir
      EnvelopePreview(
        envelope: d.envelope,
        senderName: d.senderName,
        senderLine: d.senderAddress.line,
        senderPlace: place(d.senderAddress),
        recipientName: d.recipientName,
        recipientNote: d.recipientType == 'Inmate'
            ? [
                if ((d.recipientFatherName ?? '').isNotEmpty) 'Baba adı: ${d.recipientFatherName}',
                if ((d.recipientWard ?? '').isNotEmpty) 'Koğuş: ${d.recipientWard}',
              ].join(' · ')
            : null,
        recipientDetail: d.recipientType == 'Standard' ? null : d.recipientDetail,
        recipientLine: d.recipientAddress.line,
        recipientPlace: place(d.recipientAddress),
        weightGrams: s.weights?.estimate(
          letterSheets: s.pageCount(d.content),
          photos: _photos.length,
          postcards: d.postcardIds.length,
          documentSheets: _documents.fold(0, (sum, x) => sum + x.pageCount),
        ),
      ),
      const SizedBox(height: 12),
      label('Mektup', 0),
      Container(
        padding: const EdgeInsets.fromLTRB(18, 18, 18, 12),
        decoration: BoxDecoration(
          color: LetterLook.paperColor(d.paper),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: Brand.line),
          boxShadow: const [BoxShadow(color: Color(0x14000000), blurRadius: 12, offset: Offset(0, 4))],
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text(d.content.isEmpty ? 'Mektup metni yazılmadı.' : d.content, style: style),
        ]),
      ),
      const SizedBox(height: 12),
      label('Ekstralar', 1),
      SectionCard(
        title: 'Eklenenler',
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          InfoRow('Koku', _scentName(s, d.scentId)),
          const SizedBox(height: 8),
          Text('Kartpostal', style: TextStyle(color: Brand.muted)),
          const SizedBox(height: 4),
          if (postcards.isEmpty)
            const Text('Yok')
          else
            Wrap(spacing: 8, runSpacing: 8, children: [
              for (final pc in postcards)
                SizedBox(
                  width: 90,
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    PostcardTile(imageUrl: pc.imageUrl == null ? null : client.absolute(pc.imageUrl!), width: 90),
                    Text(pc.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 11)),
                  ]),
                ),
            ]),
          const SizedBox(height: 10),
          Text('Fotoğraflar (${_photos.length})', style: TextStyle(color: Brand.muted)),
          const SizedBox(height: 4),
          if (_photos.isEmpty)
            const Text('Yok')
          else
            Wrap(spacing: 6, runSpacing: 6, children: [
              for (final (i, ph) in _photos.indexed)
                GestureDetector(
                  onTap: () => showPhotoViewer(context,
                      urls: [for (final x in _photos) client.absolute(x.url)],
                      titles: [for (final x in _photos) x.fileName],
                      initialIndex: i,
                      headers: client.authHeaders),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: Image.network(client.absolute(ph.url),
                        headers: client.authHeaders,
                        width: 60,
                        height: 60,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => Container(width: 60, height: 60, color: Brand.paper2)),
                  ),
                ),
            ]),
          const SizedBox(height: 10),
          Text('Belgeler (${_documents.length})', style: TextStyle(color: Brand.muted)),
          if (_documents.isEmpty) const Text('Yok'),
          for (final doc in _documents)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Row(children: [
                Icon(doc.isPdf ? Icons.picture_as_pdf_outlined : Icons.image_outlined, size: 18, color: Brand.muted),
                const SizedBox(width: 6),
                Expanded(child: Text(doc.fileName, maxLines: 1, overflow: TextOverflow.ellipsis)),
              ]),
            ),
        ]),
      ),
    ]);
  }
}

class _StepBar extends StatefulWidget {
  const _StepBar({required this.steps, required this.current, required this.onTap});
  final List<String> steps;
  final int current;
  final void Function(int) onTap;

  @override
  State<_StepBar> createState() => _StepBarState();
}

class _StepBarState extends State<_StepBar> {
  late final _keys = List.generate(widget.steps.length, (_) => GlobalKey());

  @override
  void didUpdateWidget(_StepBar old) {
    super.didUpdateWidget(old);
    // Dar ekranlarda aktif adım görünür kalsın.
    if (old.current != widget.current) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final ctx = _keys[widget.current].currentContext;
        if (ctx != null) {
          Scrollable.ensureVisible(ctx, alignment: 0.5, duration: const Duration(milliseconds: 250));
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final steps = widget.steps;
    final current = widget.current;
    return SizedBox(
      height: 56,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        child: Row(children: [
          for (var i = 0; i < steps.length; i++) ...[
            if (i > 0) const SizedBox(width: 6),
            _item(i, steps, current),
          ],
        ]),
      ),
    );
  }

  Widget _item(int i, List<String> steps, int current) {
    final done = i < current;
    final active = i == current;
    return InkWell(
      key: _keys[i],
      borderRadius: BorderRadius.circular(20),
      onTap: () => widget.onTap(i),
      child: Container(
        height: 36,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: active ? Theme.of(context).colorScheme.primary : (done ? Brand.burgundy.withValues(alpha: 0.12) : Brand.card),
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: active ? Brand.burgundy : Brand.line),
        ),
        child: Text(
          '${i + 1}. ${steps[i]}',
          style: TextStyle(
            color: active ? Colors.white : (done ? Brand.burgundy : Brand.muted),
            fontWeight: active ? FontWeight.w700 : FontWeight.w500,
          ),
        ),
      ),
    );
  }
}
