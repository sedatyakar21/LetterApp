import 'package:flutter/foundation.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';
import 'payment_flow.dart';

/// Mektup Yaz'ın Ödeme adımı (sitedeki Ödeme sekmesiyle aynı): üstte ödeme kalemleri, yöntem, MektupPuan ve kupon;
/// altta PayTR'nin güvenli kart formu uygulama içinde gömülü (kart bilgisi sunucumuza gelmez).
/// Adım açılınca ödeme başlatılır (mektup kilitlenir); kupon / puan / yöntem değişirse deneme kapatılıp yenisi başlar.
/// Adımdan geri çıkılırken [PaymentStepState.leave] çağrılmalıdır.
class PaymentStep extends StatefulWidget {
  const PaymentStep({super.key, required this.letterId, required this.onPaid});
  final String letterId;

  /// Ödeme tamamlandı ([free]: kart gerekmeden — puan / kampanya).
  final void Function(bool free) onPaid;

  @override
  State<PaymentStep> createState() => PaymentStepState();
}

class PaymentStepState extends State<PaymentStep> {
  final _coupon = TextEditingController();
  String _method = 'Card';
  bool _usePoints = false;
  String? _appliedCoupon;
  Quote? _quote;
  PayResult? _started;
  WebViewController? _web;
  bool _busy = false;
  bool _resultHandled = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _start());
  }

  @override
  void dispose() {
    _coupon.dispose();
    super.dispose();
  }

  /// Açık ödeme denemesini kapatır (mektup yeniden düzenlenebilir). Ödeme yapılmış olabileceği için önce sorulur.
  Future<bool> leave() async {
    if (_started == null) return true;
    final ok = await confirm(context,
        title: 'Ödeme formu kapatılsın mı?',
        message: 'Mektubunuz yeniden düzenlenebilir olacak. Ödemeyi tamamladıysanız geri dönmeyin; sonuç birkaç saniye içinde gelir.',
        ok: 'Kapat');
    if (!ok || !mounted) return false;
    return _abandon();
  }

  Future<bool> _abandon() async {
    if (_started == null) return true;
    try {
      await AppScope.read(context).api.abandonPayment(widget.letterId);
      _started = null;
      _web = null;
      return true;
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
      return false;
    }
  }

  /// Tutarı alır ve ödemeyi başlatır: tutar varsa kart formu açılır, yoksa "Siparişi tamamla" gösterilir.
  Future<void> _start() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
      _resultHandled = false;
    });
    final api = AppScope.read(context).api;
    try {
      if (!await _abandon()) return;
      final q = await api.quote(widget.letterId, method: _method, usePoints: _usePoints, coupon: _appliedCoupon);
      if (!mounted) return;
      setState(() => _quote = q);
      if (q.amountToPay <= 0) return; // ücretsiz: düğmeyle tamamlanır
      final r = await api.pay(widget.letterId,
          method: _method, usePoints: _usePoints, coupon: q.couponApplied ? q.couponCode : null);
      if (!mounted) return;
      if (r.completed) return widget.onPaid(true);
      setState(() {
        _started = r;
        _web = r.formUrl == null || kIsWeb ? null : _createWebView(r);
      });
    } catch (e) {
      if (mounted) setState(() => _error = errorText(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  WebViewController _createWebView(PayResult r) => WebViewController()
    ..setJavaScriptMode(JavaScriptMode.unrestricted)
    ..setNavigationDelegate(NavigationDelegate(
      onNavigationRequest: (req) {
        if (req.url.startsWith(r.returnUrl)) {
          _onFormFinished();
          return NavigationDecision.prevent;
        }
        return NavigationDecision.navigate;
      },
      onUrlChange: (c) {
        if (c.url != null && c.url!.startsWith(r.returnUrl)) _onFormFinished();
      },
    ))
    ..loadRequest(Uri.parse(r.formUrl!));

  Future<void> _completeFree() async {
    setState(() => _busy = true);
    try {
      final r = await AppScope.read(context)
          .api
          .pay(widget.letterId, method: _method, usePoints: _usePoints, coupon: _quote?.couponApplied == true ? _quote!.couponCode : null);
      if (mounted && r.completed) widget.onPaid(true);
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _simulate(bool success) async {
    final s = _started;
    if (s == null) return;
    setState(() => _busy = true);
    try {
      await AppScope.read(context).api.simulatePayment(s.merchantOid, success);
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
    await _onFormFinished();
  }

  /// Form bitti (dönüş adresine gelindi): kesin sonuç sunucudan sorgulanır.
  Future<void> _onFormFinished() async {
    final s = _started;
    if (s == null || _resultHandled || !mounted) return;
    _resultHandled = true;
    final (outcome, reason) = await PaymentFlow.waitForResult(context, s.merchantOid);
    if (!mounted) return;
    AppScope.read(context).lettersChanged();
    switch (outcome) {
      case PaymentOutcome.succeeded:
        _started = null;
        widget.onPaid(false);
      case PaymentOutcome.failed:
        // Deneme kapandı, mektup taslağa döndü: yeni deneme başlatılabilir.
        setState(() {
          _started = null;
          _web = null;
          _error = '${reason ?? 'Banka işlemi onaylamadı.'} Kartınızdan tahsilat yapılmadı; tekrar deneyebilirsiniz.';
        });
      case PaymentOutcome.pending:
      case PaymentOutcome.cancelled:
        _resultHandled = false;
        showMessage(context, 'Ödeme sonucu henüz gelmedi. Birazdan mektup ekranında güncellenecek.');
    }
  }

  void _applyCoupon() {
    FocusScope.of(context).unfocus();
    final code = _coupon.text.trim().toUpperCase();
    _appliedCoupon = code.isEmpty ? null : code;
    _start();
  }

  @override
  Widget build(BuildContext context) {
    final q = _quote;
    final settings = AppScope.of(context).settings!;
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      if (_busy) const LinearProgressIndicator(minHeight: 2),
      if (q != null) ...[
        if (q.mobilePaymentAvailable) ...[
          SegmentedButton<String>(
            segments: const [
              ButtonSegment(value: 'Card', icon: Icon(Icons.credit_card), label: Text('Kart')),
              ButtonSegment(value: 'Mobile', icon: Icon(Icons.phone_iphone), label: Text('Mobil ödeme')),
            ],
            selected: {_method},
            onSelectionChanged: _busy
                ? null
                : (v) {
                    _method = v.first;
                    _start();
                  },
          ),
          if (_method == 'Mobile')
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text('Mobil ödemede operatör farkı (%${(settings.prices.mobileSurchargeRate * 100).round()}) eklenir.',
                  style: const TextStyle(color: Brand.muted, fontSize: 12)),
            ),
          const SizedBox(height: 12),
        ],
        SectionCard(
          title: 'Ödeme kalemleri',
          child: Column(children: [
            for (final l in q.lines) InfoRow(l.description, money(l.amount)),
            if (q.campaignDiscount > 0)
              InfoRow('🎁 ${q.campaignTitle ?? 'Kampanya'}', '−${money(q.campaignDiscount)}', valueColor: Brand.success),
            if (q.pointsDiscount > 0)
              InfoRow('MektupPuan (${number(q.pointsUsed)} puan)', '−${money(q.pointsDiscount)}', valueColor: Brand.success),
            const Divider(height: 20),
            InfoRow('Ödenecek tutar', money(q.amountToPay), bold: true),
            if (q.pointsToEarn > 0)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text('Mektubunuz postaya verilince ${number(q.pointsToEarn)} MektupPuan kazanacaksınız.',
                    style: const TextStyle(color: Brand.muted, fontSize: 12)),
              ),
          ]),
        ),
        const SizedBox(height: 12),
        SectionCard(
          title: 'Kupon ve MektupPuan',
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Row(children: [
              Expanded(
                child: TextField(
                  controller: _coupon,
                  textCapitalization: TextCapitalization.characters,
                  decoration: const InputDecoration(hintText: 'Varsa kupon kodunuz'),
                  onSubmitted: (_) => _applyCoupon(),
                ),
              ),
              const SizedBox(width: 8),
              OutlinedButton(onPressed: _busy ? null : _applyCoupon, child: const Text('Uygula')),
            ]),
            if (q.couponApplied)
              const Padding(
                padding: EdgeInsets.only(top: 6),
                child: Text('✓ Kupon uygulandı', style: TextStyle(color: Brand.success, fontWeight: FontWeight.w600)),
              ),
            if (q.couponMessage != null)
              Padding(
                padding: const EdgeInsets.only(top: 6),
                child: Text(q.couponMessage!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              ),
            if (q.pointsBalance > 0)
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                value: _usePoints,
                onChanged: _busy
                    ? null
                    : (v) {
                        _usePoints = v;
                        _start();
                      },
                title: const Text('MektupPuan kullan', style: TextStyle(fontWeight: FontWeight.w700)),
                subtitle: Text('Bakiye: ${number(q.pointsBalance)} puan (${money(q.balanceValue)})'),
              ),
          ]),
        ),
        const SizedBox(height: 12),
      ],
      SectionCard(
        title: '🔒 Kart bilgileri',
        child: _paymentArea(q),
      ),
    ]);
  }

  Widget _paymentArea(Quote? q) {
    if (_error != null) {
      return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        const SizedBox(height: 8),
        OutlinedButton.icon(onPressed: _busy ? null : _start, icon: const Icon(Icons.refresh), label: const Text('Tekrar dene')),
      ]);
    }
    if (q == null) return const Text('Ödeme formu hazırlanıyor…', style: TextStyle(color: Brand.muted));
    if (q.amountToPay <= 0) {
      return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text(q.pointsUsed > 0
            ? 'Tutarın tamamı MektupPuan ile karşılanıyor; kart bilgisi gerekmez.'
            : 'Kampanya indirimiyle ödenecek tutar yok; kart bilgisi gerekmez.'),
        const SizedBox(height: 10),
        FilledButton.icon(
          onPressed: _busy ? null : _completeFree,
          icon: const Icon(Icons.send),
          label: Text(q.pointsUsed > 0 ? 'Puanla Öde ve Gönder' : 'Ücretsiz Gönder'),
        ),
      ]);
    }
    final s = _started;
    if (s == null) return const Text('Ödeme formu hazırlanıyor…', style: TextStyle(color: Brand.muted));
    if (s.formUrl == null && s.simulation) {
      // Test modu: gerçek form yerine sonuç seçilir (gerçek tahsilat yapılmaz).
      return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(color: const Color(0xFFFFF1D6), borderRadius: BorderRadius.circular(10)),
          child: Text('Test modu: gerçek ödeme alınmaz. ${money(q.amountToPay)} tutarındaki ödemenin sonucunu seçin.',
              style: const TextStyle(color: Color(0xFF8A5A00))),
        ),
        const SizedBox(height: 10),
        FilledButton.icon(
          style: FilledButton.styleFrom(backgroundColor: Brand.success),
          onPressed: _busy ? null : () => _simulate(true),
          icon: const Icon(Icons.check_circle_outline),
          label: const Text('Ödeme başarılı'),
        ),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: _busy ? null : () => _simulate(false),
          icon: const Icon(Icons.cancel_outlined),
          label: const Text('Banka reddetti'),
        ),
      ]);
    }
    final web = _web;
    if (web == null) {
      // Web önizlemesinde gömülü tarayıcı yok: form yeni sekmede açılır.
      return FilledButton.icon(
        onPressed: () async {
          final (outcome, _) = await PaymentFlow.complete(context,
              merchantOid: s.merchantOid, amount: q.amountToPay, formUrl: s.formUrl, simulation: s.simulation, returnUrl: s.returnUrl);
          if (outcome == PaymentOutcome.succeeded && mounted) widget.onPaid(false);
        },
        icon: const Icon(Icons.lock_outline),
        label: Text('${money(q.amountToPay)} Öde'),
      );
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      const Text('Kart bilgileriniz güvenli ödeme altyapısı PayTR üzerinden alınır; bizim sistemimizde saklanmaz.',
          style: TextStyle(color: Brand.muted, fontSize: 12)),
      const SizedBox(height: 8),
      SizedBox(
        height: 560,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(8),
          // Form kendi içinde kaydırılabilsin (sayfa kaydırmasıyla çakışmasın).
          child: WebViewWidget(
            controller: web,
            gestureRecognizers: {Factory<OneSequenceGestureRecognizer>(() => EagerGestureRecognizer())},
          ),
        ),
      ),
    ]);
  }
}
