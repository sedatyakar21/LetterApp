import 'package:flutter/material.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/address_fields.dart';
import '../../ui/common.dart';
import '../../ui/letter_style.dart';
import '../../ui/theme.dart';

/// Düzenleyiciden dönüş: kaydedilen mektup ve ödemeye geçilecek mi.
class EditorOutcome {
  const EditorOutcome(this.letter, {this.checkout = false});
  final Letter letter;
  final bool checkout;
}

/// Mektup Yaz / Düzenle: Alıcı → Mektup → Görünüm → Gönderen → Özet.
class LetterEditorScreen extends StatefulWidget {
  const LetterEditorScreen({super.key, this.letter});
  final Letter? letter;

  @override
  State<LetterEditorScreen> createState() => _LetterEditorScreenState();
}

class _LetterEditorScreenState extends State<LetterEditorScreen> {
  // İlk adım mektup metni (sitedeki formla aynı sıra).
  static const _steps = ['Mektup', 'Alıcı', 'Gönderen', 'Görünüm', 'Özet'];
  static const _contentIndex = 0, _recipientIndex = 1, _senderIndex = 2;

  final _forms = List.generate(5, (_) => GlobalKey<FormState>());
  late final LetterDraft _draft;
  late final TextEditingController _recipientName, _recipientDetail, _fatherName, _ward, _content, _senderName;
  late final AddressControllers _recipientAddress, _senderAddress;
  int _step = 0;
  bool _busy = false;
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

  void _next() {
    if (_forms[_step].currentState?.validate() == false) return;
    setState(() => _step++);
  }

  Future<void> _save({required bool checkout}) async {
    // Taslak olarak kaydederken de alıcı ve gönderen bilgileri zorunludur (sunucu kuralı).
    final draft = _collect();
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
    if (checkout && draft.content.trim().isEmpty) {
      setState(() => _step = _contentIndex);
      showMessage(context, 'Ödemeye geçmeden önce mektubunuzu yazın.', error: true);
      return;
    }
    setState(() => _busy = true);
    final session = AppScope.read(context);
    try {
      final saved = await session.api.saveLetter(widget.letter?.id, draft);
      session.lettersChanged();
      _dirty = false;
      if (!mounted) return;
      if (!checkout) showMessage(context, 'Taslak kaydedildi.');
      Navigator.of(context).pop(EditorOutcome(saved, checkout: checkout));
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _confirmLeave() async {
    if (!_dirty) return true;
    return confirm(context,
        title: 'Değişiklikler kaydedilmedi',
        message: 'Çıkarsanız yazdıklarınız kaybolacak. Önce "Taslak kaydet" ile kaydedebilirsiniz.',
        ok: 'Çık',
        danger: true);
  }

  @override
  Widget build(BuildContext context) {
    final settings = AppScope.of(context).settings!;
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        if (_step > 0) {
          setState(() => _step--);
          return;
        }
        if (await _confirmLeave() && context.mounted) Navigator.of(context).pop();
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.letter == null ? 'Mektup Yaz' : 'Mektubu Düzenle'),
          actions: [
            TextButton(onPressed: _busy ? null : () => _save(checkout: false), child: const Text('Taslak kaydet')),
          ],
          bottom: PreferredSize(preferredSize: const Size.fromHeight(56), child: _StepBar(steps: _steps, current: _step, onTap: (i) {
            if (i < _step || (_forms[_step].currentState?.validate() ?? true)) setState(() => _step = i);
          })),
        ),
        body: SafeArea(
          child: IndexedStack(index: _step, children: [
            _page(0, _contentStep(settings)),
            _page(1, _recipientStep(settings)),
            _page(2, _senderStep()),
            _page(3, _styleStep(settings)),
            _page(4, _summaryStep(settings)),
          ]),
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
            child: Row(children: [
              if (_step > 0)
                Expanded(
                  child: OutlinedButton(onPressed: _busy ? null : () => setState(() => _step--), child: const Text('Geri')),
                ),
              if (_step > 0) const SizedBox(width: 12),
              Expanded(
                flex: 2,
                child: _step < _steps.length - 1
                    ? FilledButton(onPressed: _next, child: Text('Devam: ${_steps[_step + 1]}'))
                    : FilledButton.icon(
                        onPressed: _busy ? null : () => _save(checkout: true),
                        icon: _busy
                            ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                            : const Icon(Icons.lock_outline),
                        label: const Text('Kaydet ve Ödemeye Geç'),
                      ),
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

  // ---------------- 1. Alıcı ----------------

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

  // ---------------- 2. Mektup ----------------

  Widget _contentStep(AppSettings s) {
    final pages = s.pageCount(_content.text);
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Container(
        decoration: BoxDecoration(
          color: LetterLook.paperColor(_draft.paper),
          borderRadius: BorderRadius.circular(6),
          border: Border.all(color: Brand.line),
          boxShadow: const [BoxShadow(color: Color(0x14000000), blurRadius: 12, offset: Offset(0, 4))],
        ),
        padding: const EdgeInsets.fromLTRB(18, 18, 18, 8),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          TextFormField(
            controller: _content,
            minLines: 14,
            maxLines: null,
            maxLength: s.maxContentLength,
            keyboardType: TextInputType.multiline,
            textCapitalization: TextCapitalization.sentences,
            style: LetterLook.textStyle(_draft.font, _draft.ink),
            decoration: const InputDecoration(
              hintText: 'Sevgili ...\n\nBuraya yazdıklarınız kâğıda basılıp zarflanacak.',
              filled: false,
              border: InputBorder.none,
              enabledBorder: InputBorder.none,
              focusedBorder: InputBorder.none,
              counterText: '',
              contentPadding: EdgeInsets.zero,
            ),
          ),
          Align(
            alignment: Alignment.centerRight,
            child: Text('— ${_senderName.text.isEmpty ? 'Adınız' : _senderName.text}',
                style: LetterLook.textStyle(_draft.font, _draft.ink)),
          ),
        ]),
      ),
      const SizedBox(height: 10),
      Row(children: [
        Icon(Icons.description_outlined, size: 16, color: Brand.muted.withValues(alpha: 0.9)),
        const SizedBox(width: 6),
        Text('$pages sayfa', style: const TextStyle(color: Brand.muted)),
        if (pages > 1)
          Text(' (+${money((pages - 1) * s.prices.extraPage)})', style: const TextStyle(color: Brand.muted)),
        const Spacer(),
        Text('${_content.text.length} / ${s.maxContentLength}', style: const TextStyle(color: Brand.muted, fontSize: 12)),
      ]),
      const SizedBox(height: 4),
      Text('İlk sayfa fiyata dahildir; her ek sayfa ${money(s.prices.extraPage)}.',
          style: const TextStyle(color: Brand.muted, fontSize: 12)),
    ]);
  }

  // ---------------- 3. Görünüm ve ekstralar ----------------

  Widget _styleStep(AppSettings s) {
    final p = s.prices;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionCard(
        title: 'Kâğıt rengi',
        trailing: Text('Renkli: +${money(p.coloredPaper)}', style: const TextStyle(color: Brand.muted, fontSize: 12)),
        child: _chips(s.paperColors, _draft.paper, (v) => _draft.paper = v,
            avatar: (v) => Swatch(LetterLook.paperColor(v))),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Zarf rengi',
        trailing: Text('Renkli: +${money(p.coloredEnvelope)}', style: const TextStyle(color: Brand.muted, fontSize: 12)),
        child: _chips(s.envelopeColors, _draft.envelope, (v) => _draft.envelope = v,
            avatar: (v) => Swatch(LetterLook.envelope[v] ?? Colors.white)),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Yazı stili ve mürekkep',
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          _chips(s.fonts, _draft.font, (v) => _draft.font = v),
          const SizedBox(height: 10),
          _chips(s.inks, _draft.ink, (v) => _draft.ink = v, avatar: (v) => Swatch(LetterLook.inkColor(v))),
        ]),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Koku',
        trailing: Text('+${money(p.scent)}', style: const TextStyle(color: Brand.muted, fontSize: 12)),
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
          trailing: Text('her biri +${money(p.postcard)}', style: const TextStyle(color: Brand.muted, fontSize: 12)),
          child: _postcardPicker(s),
        ),
      ],
    ]);
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
        Text(g.key, style: const TextStyle(color: Brand.muted, fontSize: 13)),
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
                        const Positioned(
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

  // ---------------- 4. Gönderen ----------------

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
      const Text('Alıcı cevap yazmak isterse zarfın üzerindeki gönderen adresini kullanır.',
          style: TextStyle(color: Brand.muted, fontSize: 13)),
    ]);
  }

  // ---------------- 5. Özet ----------------

  Widget _summaryStep(AppSettings s) {
    final d = _collect();
    final p = s.prices;
    final pages = s.pageCount(d.content);
    final photos = widget.letter?.photos.length ?? 0;
    final documents = widget.letter?.documents.length ?? 0;
    final lines = <(String, double)>[
      ('Mektup (1 sayfa, zarf ve kayıtlı posta)', p.basePrice),
      if (pages > 1) ('Ek sayfa × ${pages - 1}', p.extraPage * (pages - 1)),
      if (photos > 0) ('Fotoğraf × $photos', p.photo * photos),
      if (documents > 0) ('Belge × $documents', p.document * documents),
      if (d.postcardIds.isNotEmpty)
        (d.postcardIds.length == 1 ? 'Kartpostal' : 'Kartpostal × ${d.postcardIds.length}', p.postcard * d.postcardIds.length),
      if (d.paper != 'White') ('Renkli kâğıt', p.coloredPaper),
      if (d.envelope != 'White') ('Renkli zarf', p.coloredEnvelope),
      if (d.scentId != null) ('Koku', p.scent),
    ];
    final total = lines.fold<double>(0, (sum, l) => sum + l.$2);
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      SectionCard(
        title: 'Alıcı',
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(d.recipientName.isEmpty ? '—' : d.recipientName, style: const TextStyle(fontWeight: FontWeight.w700)),
          if (d.recipientType != 'Standard' && d.recipientDetail != null) Text(d.recipientDetail!),
          if (d.recipientType == 'Inmate' && (d.recipientFatherName != null || d.recipientWard != null))
            Text([
              if (d.recipientFatherName != null) 'Baba adı: ${d.recipientFatherName}',
              if (d.recipientWard != null) 'Koğuş: ${d.recipientWard}',
            ].join(' · ')),
          Text(d.recipientAddress.isEmpty ? '—' : d.recipientAddress.toString(), style: const TextStyle(color: Brand.muted)),
        ]),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Seçimler',
        child: Column(children: [
          InfoRow('Kâğıt', s.label(s.paperColors, d.paper)),
          InfoRow('Zarf', s.label(s.envelopeColors, d.envelope)),
          InfoRow('Yazı', '${s.label(s.fonts, d.font)} · ${s.label(s.inks, d.ink)}'),
          InfoRow('Koku', _scentName(s, d.scentId)),
          InfoRow('Kartpostal', switch (_selectedPostcards(s)) {
            [] => 'Yok',
            final list => list.map((pc) => pc.title).join(', '),
          }),
          InfoRow('Sayfa', '$pages'),
        ]),
      ),
      const SizedBox(height: 12),
      SectionCard(
        title: 'Tahmini tutar',
        child: Column(children: [
          for (final l in lines) InfoRow(l.$1, money(l.$2)),
          const Divider(height: 20),
          InfoRow('Toplam', money(total), bold: true),
          const SizedBox(height: 6),
          const Text(
            'Kampanya indirimi, MektupPuan ve ödeme yöntemi bir sonraki ekranda uygulanır. '
            'Fotoğraf ve belgeleri mektubu kaydettikten sonra detay ekranından ekleyebilirsiniz.',
            style: TextStyle(color: Brand.muted, fontSize: 12),
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
          color: active ? Brand.burgundy : (done ? Brand.burgundy.withValues(alpha: 0.1) : Colors.white),
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
