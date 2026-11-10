import 'package:flutter/material.dart';

import '../api/models.dart';
import '../state/session.dart';
import '../ui/common.dart';
import '../ui/theme.dart';

/// Gönderi takibi (giriş gerektirmez): PTT takip numarasıyla mektubun durumu.
class TrackingScreen extends StatefulWidget {
  const TrackingScreen({super.key, this.standalone = false});
  final bool standalone;

  @override
  State<TrackingScreen> createState() => _TrackingScreenState();
}

class _TrackingScreenState extends State<TrackingScreen> {
  final _code = TextEditingController();
  Tracking? _result;
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  Future<void> _search() async {
    FocusScope.of(context).unfocus();
    final code = _code.text.trim();
    if (code.isEmpty) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final r = await AppScope.read(context).api.track(code);
      setState(() => _result = r);
    } catch (e) {
      setState(() {
        _result = null;
        _error = errorText(e);
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final r = _result;
    return Scaffold(
      appBar: AppBar(title: const Text('Gönderi Takibi'), automaticallyImplyLeading: widget.standalone),
      body: ListView(padding: const EdgeInsets.all(16), children: [
        SectionCard(
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            const Text('Mektubun takip numarasını girin', style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            const Text('Takip numarası mektup postaya verildiğinde SMS / e-posta ile gönderilir.',
                style: TextStyle(color: Brand.muted, fontSize: 13)),
            const SizedBox(height: 12),
            TextField(
              controller: _code,
              textCapitalization: TextCapitalization.characters,
              textInputAction: TextInputAction.search,
              onSubmitted: (_) => _search(),
              decoration: const InputDecoration(hintText: 'Ör. RR123456789TR', prefixIcon: Icon(Icons.qr_code_2)),
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _busy ? null : _search,
              child: _busy
                  ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                  : const Text('Sorgula'),
            ),
          ]),
        ),
        if (_error != null) ...[
          const SizedBox(height: 16),
          EmptyView(icon: Icons.search_off, title: 'Bulunamadı', message: _error),
        ],
        if (r != null) ...[
          const SizedBox(height: 16),
          SectionCard(
            title: r.trackingCode,
            trailing: StatusChip(status: r.status, label: r.statusLabel),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              InfoRow('Alıcı', r.recipientInitials),
              InfoRow('Şehir', r.city),
              const SizedBox(height: 12),
              Timeline(items: [
                for (final h in r.history.reversed) (title: h.statusLabel, note: h.note, at: h.at, status: h.status),
              ]),
            ]),
          ),
        ],
      ]),
    );
  }
}
