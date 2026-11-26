import 'package:flutter/material.dart';

import '../api/models.dart';
import '../state/session.dart';
import 'theme.dart';

/// Adres alanlarının denetleyicileri (adres, ilçe, il, posta kodu).
class AddressControllers {
  AddressControllers([Address a = const Address()])
      : line = TextEditingController(text: a.line),
        district = TextEditingController(text: a.district),
        city = TextEditingController(text: a.city),
        postalCode = TextEditingController(text: a.postalCode);

  final TextEditingController line, district, city, postalCode;

  Address get value => Address(
      line: line.text.trim(), district: district.text.trim(), city: city.text.trim(), postalCode: postalCode.text.trim());

  void addListener(VoidCallback l) {
    for (final c in [line, district, city, postalCode]) {
      c.addListener(l);
    }
  }

  void dispose() {
    for (final c in [line, district, city, postalCode]) {
      c.dispose();
    }
  }
}

/// Web formundaki kurallarla aynı doğrulamalı adres alanları. İl ve ilçe aramalı listeden seçilir.
/// [prisonMode] açıkken (alıcı türü "Cezaevi") il "Alıcı Cezaevi" olarak etiketlenir, ilin cezaevleri ayrı listede
/// gösterilir; seçilen kurumun ilçe, adres ve posta kodu doldurulur ve [onPrisonPicked] çağrılır.
class AddressFields extends StatefulWidget {
  const AddressFields({super.key, required this.controllers, this.prisonMode = false, this.onPrisonPicked});
  final AddressControllers controllers;
  final bool prisonMode;
  final void Function(PrisonOption prison)? onPrisonPicked;

  @override
  State<AddressFields> createState() => _AddressFieldsState();
}

class _AddressFieldsState extends State<AddressFields> {
  static const _manual = 'Listede yok, adresi elle gireceğim';
  static final _prisonCache = <String, Future<List<PrisonOption>>>{};

  bool _normalized = false;
  String _prison = '';
  List<PrisonOption> _prisons = const [];
  bool _loadingPrisons = false;

  AddressControllers get c => widget.controllers;

  @override
  void initState() {
    super.initState();
    if (widget.prisonMode) WidgetsBinding.instance.addPostFrameCallback((_) => _loadPrisons(preselect: true));
  }

  @override
  void didUpdateWidget(AddressFields old) {
    super.didUpdateWidget(old);
    if (widget.prisonMode && !old.prisonMode) _loadPrisons(preselect: true);
  }

  Future<void> _loadPrisons({bool preselect = false}) async {
    final city = c.city.text;
    if (!widget.prisonMode || city.isEmpty) {
      setState(() => _prisons = const []);
      return;
    }
    setState(() => _loadingPrisons = true);
    final api = AppScope.read(context).api;
    try {
      final list = await (_prisonCache[city] ??= api.prisons(city).catchError((Object e) {
        _prisonCache.remove(city);
        throw e;
      }));
      if (!mounted || c.city.text != city) return;
      setState(() {
        _prisons = list;
        if (preselect) {
          // Kayıtlı adres bir kuruma aitse onu seçili göster.
          final key = Locations.fold(c.line.text);
          final hit = list.where((p) => p.addressLine.isNotEmpty && Locations.fold(p.addressLine) == key && p.district == c.district.text);
          _prison = hit.isNotEmpty ? hit.first.name : (c.line.text.isEmpty ? '' : _manual);
        }
      });
    } catch (_) {
      if (mounted) setState(() => _prisons = const []);
    } finally {
      if (mounted) setState(() => _loadingPrisons = false);
    }
  }

  void _pickPrison(String name) {
    setState(() => _prison = name);
    final p = _prisons.where((x) => x.name == name).firstOrNull;
    if (p == null) return;
    setState(() {
      if (p.district.isNotEmpty) c.district.text = p.district;
      if (p.addressLine.isNotEmpty) c.line.text = p.addressLine;
      c.postalCode.text = p.postalCode;
    });
    widget.onPrisonPicked?.call(p);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_normalized) return;
    _normalized = true;
    // Eski kayıtlardaki yazımı listedekine çevir ("Istanbul" → "İstanbul", "Kadikoy" → "Kadıköy").
    final loc = AppScope.read(context).locations;
    final p = loc.find(c.city.text);
    if (p != null) {
      if (c.city.text != p.name) c.city.text = p.name;
      final d = loc.findDistrict(p, c.district.text);
      if (d != null && c.district.text != d) c.district.text = d;
    }
  }

  static String? _required(String? v, String name) => (v ?? '').trim().isEmpty ? '$name zorunludur.' : null;

  @override
  Widget build(BuildContext context) {
    final loc = AppScope.of(context).locations;
    final province = loc.find(c.city.text);
    // Listede olmayan eski bir değer seçilebilir kalsın.
    List<String> withCurrent(List<String> items, String current) =>
        current.isEmpty || items.contains(current) ? items : [current, ...items];

    final prisonMode = widget.prisonMode;
    // Listeden kurum seçiliyse ilçe ve adres kurumdan gelir ve düzenlenemez ("elle gireceğim" seçilince açılır).
    final picked = prisonMode ? _prisons.where((p) => p.name == _prison).firstOrNull : null;
    final districtLocked = picked != null && picked.district.isNotEmpty;
    final lineLocked = picked != null && picked.addressLine.isNotEmpty;
    // Sıra: il, (cezaevi), ilçe, adres. Posta kodu şimdilik gizli; kayıtlı değer denetleyicide korunur ve sunucuya gider.
    return Column(children: [
      PickerField(
        key: ValueKey('city-${c.city.text}-$prisonMode'),
        label: prisonMode ? 'Alıcı Cezaevi' : 'İl',
        value: c.city.text,
        items: withCurrent(loc.names, c.city.text),
        searchHint: 'İl ara',
        validator: (v) => _required(v, 'İl'),
        onChanged: (v) {
          if (v == c.city.text) return;
          setState(() {
            // Önceki ilin kurumundan gelen adres yeni ile ait olamaz.
            if (prisonMode && _prison.isNotEmpty && _prison != _manual) c.line.clear();
            c.city.text = v;
            c.district.clear();
            _prison = '';
          });
          _loadPrisons();
        },
      ),
      if (prisonMode) ...[
        const SizedBox(height: 12),
        PickerField(
          key: ValueKey('prison-${c.city.text}-$_prison-${_prisons.length}'),
          label: 'Cezaevi',
          value: _prison,
          items: [..._prisons.map((p) => p.name), _manual],
          enabled: c.city.text.isNotEmpty && !_loadingPrisons,
          disabledHint: _loadingPrisons ? 'Yükleniyor…' : 'Önce il seçin',
          searchHint: 'Cezaevi ara',
          onChanged: _pickPrison,
        ),
        Container(
          alignment: Alignment.centerLeft,
          padding: const EdgeInsets.only(top: 4, left: 4),
          child: Text(
            c.city.text.isNotEmpty && !_loadingPrisons && _prisons.isEmpty
                ? 'Bu ilde kayıtlı cezaevi yok; adresi elle girin.'
                : 'Seçince ilçe ve adres otomatik dolar.',
            style: TextStyle(color: Brand.muted, fontSize: 12),
          ),
        ),
      ],
      const SizedBox(height: 12),
      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Expanded(
          child: PickerField(
            key: ValueKey('district-${c.city.text}-${c.district.text}-$districtLocked'),
            label: 'İlçe',
            value: c.district.text,
            items: withCurrent(province?.districts ?? const [], c.district.text),
            enabled: c.city.text.isNotEmpty && !districtLocked,
            disabledHint: 'Önce il seçin',
            searchHint: 'İlçe ara',
            validator: (v) => _required(v, 'İlçe'),
            onChanged: (v) => setState(() => c.district.text = v),
          ),
        ),
      ]),
      const SizedBox(height: 12),
      TextFormField(
        controller: c.line,
        readOnly: lineLocked,
        minLines: 2,
        maxLines: 3,
        maxLength: 250,
        textCapitalization: TextCapitalization.sentences,
        autofillHints: const [AutofillHints.streetAddressLevel1],
        style: lineLocked ? TextStyle(color: Brand.muted) : null,
        decoration: InputDecoration(
          labelText: 'Adres',
          hintText: 'Mahalle, cadde/sokak, bina ve daire no',
          counterText: '',
          fillColor: lineLocked ? Brand.paper2 : null,
          suffixIcon: lineLocked
              ? const Tooltip(message: 'Cezaevi seçildiği için değiştirilemez', child: Icon(Icons.lock_outline, size: 18))
              : null,
          helperText: lineLocked || districtLocked ? 'Kurumdan geldi; değiştirmek için cezaevi listesinden "Listede yok"u seçin.' : null,
          helperMaxLines: 2,
        ),
        validator: (v) => _required(v, 'Adres'),
      ),
    ]);
  }
}

/// Uzun listeler için açılır alan: dokununca aramalı seçim ekranı açılır.
class PickerField extends FormField<String> {
  PickerField({
    super.key,
    required String label,
    required String value,
    required List<String> items,
    required void Function(String) onChanged,
    super.validator,
    super.enabled = true,
    String? disabledHint,
    String searchHint = 'Ara',
  }) : super(
          initialValue: value,
          builder: (state) {
            final context = state.context;
            final text = state.value ?? '';
            return Semantics(
              button: true,
              label: label,
              value: text,
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
                onTap: !enabled
                    ? null
                    : () async {
                        final picked = await showModalBottomSheet<String>(
                          context: context,
                          isScrollControlled: true,
                          showDragHandle: true,
                          builder: (_) => _PickerSheet(title: label, items: items, selected: text, searchHint: searchHint),
                        );
                        if (picked != null) {
                          state.didChange(picked);
                          onChanged(picked);
                        }
                      },
                child: InputDecorator(
                  isEmpty: text.isEmpty,
                  decoration: InputDecoration(
                    labelText: label,
                    hintText: enabled ? 'Seçin' : disabledHint,
                    errorText: state.errorText,
                    enabled: enabled,
                    suffixIcon: const Icon(Icons.expand_more),
                  ),
                  child: Text(text, maxLines: 1, overflow: TextOverflow.ellipsis),
                ),
              ),
            );
          },
        );
}

class _PickerSheet extends StatefulWidget {
  const _PickerSheet({required this.title, required this.items, required this.selected, required this.searchHint});
  final String title;
  final List<String> items;
  final String selected;
  final String searchHint;

  @override
  State<_PickerSheet> createState() => _PickerSheetState();
}

class _PickerSheetState extends State<_PickerSheet> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final q = Locations.fold(_query);
    // Adı aranan harflerle başlayanlar önce ("iz" → İzmir, sonra Denizli, Rize).
    final filtered = q.isEmpty
        ? widget.items
        : [
            ...widget.items.where((i) => Locations.fold(i).startsWith(q)),
            ...widget.items.where((i) => !Locations.fold(i).startsWith(q) && Locations.fold(i).contains(q)),
          ];
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.75,
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: TextField(
              autofocus: widget.items.length > 12,
              decoration: InputDecoration(hintText: widget.searchHint, prefixIcon: const Icon(Icons.search)),
              onChanged: (v) => setState(() => _query = v),
            ),
          ),
          Expanded(
            child: filtered.isEmpty
                ? Center(child: Text('Sonuç yok', style: TextStyle(color: Brand.muted)))
                : ListView.builder(
                    itemCount: filtered.length,
                    itemBuilder: (_, i) {
                      final item = filtered[i];
                      final selected = item == widget.selected;
                      return ListTile(
                        title: Text(item,
                            style: TextStyle(
                                fontWeight: selected ? FontWeight.w700 : FontWeight.w400,
                                color: selected ? Brand.burgundy : Brand.ink)),
                        trailing: selected ? Icon(Icons.check, color: Brand.burgundy) : null,
                        onTap: () => Navigator.pop(context, item),
                      );
                    },
                  ),
          ),
        ]),
      ),
    );
  }
}
