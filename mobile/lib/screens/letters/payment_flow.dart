import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

/// Ödeme sonucu: başarılı, başarısız (sebebiyle) veya henüz kesinleşmedi.
enum PaymentOutcome { succeeded, failed, pending, cancelled }

/// Başlamış bir ödemeyi tamamlatır: sağlayıcının formunu (PayTR) uygulama içinde açar
/// ya da simülasyonda test kartı seçtirir; ardından kesin sonucu sunucudan sorgular.
class PaymentFlow {
  static Future<(PaymentOutcome, String?)> complete(
    BuildContext context, {
    required String merchantOid,
    required double amount,
    String? formUrl,
    required bool simulation,
    String? returnUrl,
  }) async {
    final api = AppScope.read(context).api;

    if (formUrl != null) {
      final opened = await _openForm(context, formUrl, returnUrl);
      if (!opened) return (PaymentOutcome.cancelled, null);
    } else if (simulation) {
      final success = await showModalBottomSheet<bool>(
        context: context,
        showDragHandle: true,
        builder: (c) => _SimulationSheet(amount: amount),
      );
      if (success == null) return (PaymentOutcome.cancelled, null);
      try {
        await api.simulatePayment(merchantOid, success);
      } catch (e) {
        return (PaymentOutcome.failed, errorText(e));
      }
    } else {
      return (PaymentOutcome.pending, null);
    }

    if (!context.mounted) return (PaymentOutcome.pending, null);
    return _waitForResult(context, merchantOid);
  }

  /// Sonuç sağlayıcının sunucu bildirimiyle kesinleşir; kısa süre sorgulanır.
  static Future<(PaymentOutcome, String?)> _waitForResult(BuildContext context, String oid) async {
    final api = AppScope.read(context).api;
    final result = await showDialog<(PaymentOutcome, String?)>(
      context: context,
      barrierDismissible: false,
      builder: (c) => _PollingDialog(poll: () => api.paymentStatus(oid)),
    );
    return result ?? (PaymentOutcome.pending, null);
  }

  static Future<bool> _openForm(BuildContext context, String formUrl, String? returnUrl) async {
    if (kIsWeb) {
      // Web önizlemesinde gömülü tarayıcı yok: form yeni sekmede açılır.
      await launchUrl(Uri.parse(formUrl), webOnlyWindowName: '_blank');
      if (!context.mounted) return false;
      return confirm(context,
          title: 'Ödeme sayfası açıldı', message: 'Ödemeyi tamamladıktan sonra "Tamamladım"a dokunun.', ok: 'Tamamladım');
    }
    final done = await Navigator.of(context).push<bool>(
      MaterialPageRoute(fullscreenDialog: true, builder: (_) => PaymentWebView(formUrl: formUrl, returnUrl: returnUrl)),
    );
    return done ?? false;
  }
}

/// Ödeme sağlayıcısının güvenli ödeme sayfası. Dönüş adresine gelince kapanır.
class PaymentWebView extends StatefulWidget {
  const PaymentWebView({super.key, required this.formUrl, this.returnUrl});
  final String formUrl;
  final String? returnUrl;

  @override
  State<PaymentWebView> createState() => _PaymentWebViewState();
}

class _PaymentWebViewState extends State<PaymentWebView> {
  late final WebViewController _controller;
  int _progress = 0;
  bool _closed = false;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(NavigationDelegate(
        onProgress: (p) => setState(() => _progress = p),
        onNavigationRequest: (req) {
          if (_isReturn(req.url)) {
            _finish(true);
            return NavigationDecision.prevent;
          }
          return NavigationDecision.navigate;
        },
        onUrlChange: (change) {
          if (change.url != null && _isReturn(change.url!)) _finish(true);
        },
      ))
      ..loadRequest(Uri.parse(widget.formUrl));
  }

  bool _isReturn(String url) => widget.returnUrl != null && url.startsWith(widget.returnUrl!);

  void _finish(bool done) {
    if (_closed || !mounted) return;
    _closed = true;
    Navigator.of(context).pop(done);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Güvenli Ödeme'),
        leading: IconButton(
          tooltip: 'Kapat',
          icon: const Icon(Icons.close),
          onPressed: () async {
            final leave = await confirm(context,
                title: 'Ödeme sayfasından çıkılsın mı?',
                message: 'Ödemeyi tamamladıysanız sonucu kontrol ederiz. Tamamlamadıysanız mektup detayından tekrar ödeyebilirsiniz.',
                ok: 'Çık');
            if (leave) _finish(true);
          },
        ),
        bottom: _progress < 100
            ? PreferredSize(preferredSize: const Size.fromHeight(3), child: LinearProgressIndicator(value: _progress / 100))
            : null,
      ),
      body: WebViewWidget(controller: _controller),
    );
  }
}

class _SimulationSheet extends StatelessWidget {
  const _SimulationSheet({required this.amount});
  final double amount;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            const Icon(Icons.science_outlined, color: Brand.burgundy),
            const SizedBox(width: 8),
            Text('Test ödemesi', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700)),
          ]),
          const SizedBox(height: 8),
          Text(
            'Ödeme sağlayıcısı simülasyon modunda; gerçek para çekilmez. '
            '${money(amount)} tutarındaki ödemenin sonucunu seçin.',
            style: const TextStyle(color: Brand.muted),
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            style: FilledButton.styleFrom(backgroundColor: Brand.success),
            onPressed: () => Navigator.pop(context, true),
            icon: const Icon(Icons.check_circle_outline),
            label: const Text('Ödeme başarılı'),
          ),
          const SizedBox(height: 10),
          OutlinedButton.icon(
            onPressed: () => Navigator.pop(context, false),
            icon: const Icon(Icons.cancel_outlined),
            label: const Text('Banka reddetti'),
          ),
        ]),
      ),
    );
  }
}

class _PollingDialog extends StatefulWidget {
  const _PollingDialog({required this.poll});
  final Future<PaymentState> Function() poll;

  @override
  State<_PollingDialog> createState() => _PollingDialogState();
}

class _PollingDialogState extends State<_PollingDialog> {
  static const _maxTries = 15;
  Timer? _timer;
  int _tries = 0;

  @override
  void initState() {
    super.initState();
    _check();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _check() async {
    _tries++;
    try {
      final s = await widget.poll();
      if (!mounted) return;
      if (s.isSucceeded) return Navigator.pop(context, (PaymentOutcome.succeeded, null));
      if (!s.isPending) return Navigator.pop(context, (PaymentOutcome.failed, s.failureReason ?? s.statusLabel));
    } catch (_) {
      // Bağlantı hatasında tekrar denenir.
    }
    if (!mounted) return;
    if (_tries >= _maxTries) return Navigator.pop(context, (PaymentOutcome.pending, null));
    _timer = Timer(const Duration(seconds: 2), _check);
  }

  @override
  Widget build(BuildContext context) {
    return const AlertDialog(
      content: Row(children: [
        CircularProgressIndicator(),
        SizedBox(width: 20),
        Expanded(child: Text('Ödeme sonucu bankadan bekleniyor…')),
      ]),
    );
  }
}
