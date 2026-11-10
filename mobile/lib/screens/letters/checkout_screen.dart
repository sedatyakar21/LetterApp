import 'package:flutter/material.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';
import 'payment_flow.dart';

/// Ödeme ekranı: yöntem, MektupPuan, kupon, fiyat dökümü ve ödeme. Ödeme tamamlanınca true döner.
class CheckoutScreen extends StatefulWidget {
  const CheckoutScreen({super.key, required this.letterId});
  final String letterId;

  @override
  State<CheckoutScreen> createState() => _CheckoutScreenState();
}

class _CheckoutScreenState extends State<CheckoutScreen> {
  final _coupon = TextEditingController();
  String _method = 'Card';
  bool _usePoints = false;
  String? _appliedCoupon;
  Future<Quote>? _future;
  Quote? _quote;
  bool _paying = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _coupon.dispose();
    super.dispose();
  }

  void _load() {
    final f = AppScope.read(context)
        .api
        .quote(widget.letterId, method: _method, usePoints: _usePoints, coupon: _appliedCoupon);
    f.then((q) {
      if (mounted) setState(() => _quote = q);
    }).catchError((Object e) {
      // İlk yüklemede hata AsyncView'da görünür; sonraki güncellemelerde mesaj gösterilir.
      if (mounted && _quote != null) showMessage(context, errorText(e), error: true);
    });
    if (_future == null) {
      _future = f;
    } else {
      setState(() {
        _future = f;
      });
    }
  }

  void _applyCoupon() {
    FocusScope.of(context).unfocus();
    final code = _coupon.text.trim().toUpperCase();
    _appliedCoupon = code.isEmpty ? null : code;
    _load();
  }

  Future<void> _pay() async {
    final q = _quote;
    if (q == null) return;
    // Kupon geçersizse sunucu ödemeyi reddeder; önce müşteriyi uyar.
    if (q.couponCode != null && !q.couponApplied) {
      showMessage(context, q.couponMessage ?? 'Kupon kodu kullanılamıyor. Kaldırıp tekrar deneyin.', error: true);
      return;
    }
    setState(() => _paying = true);
    final session = AppScope.read(context);
    try {
      final r = await session.api.pay(widget.letterId, method: _method, usePoints: _usePoints, coupon: _appliedCoupon);
      session.lettersChanged();
      if (!mounted) return;
      if (r.completed) return _done(true);

      final (outcome, reason) = await PaymentFlow.complete(context,
          merchantOid: r.merchantOid,
          amount: q.amountToPay,
          formUrl: r.formUrl,
          simulation: r.simulation,
          returnUrl: r.returnUrl);
      session.lettersChanged();
      if (!mounted) return;
      switch (outcome) {
        case PaymentOutcome.succeeded:
          _done(false);
        case PaymentOutcome.failed:
          await showDialog<void>(
            context: context,
            builder: (c) => AlertDialog(
              title: const Text('Ödeme alınamadı'),
              content: Text('${reason ?? 'Banka işlemi onaylamadı.'}\n\nMektubunuz taslak olarak duruyor; tekrar deneyebilirsiniz.'),
              actions: [FilledButton(onPressed: () => Navigator.pop(c), child: const Text('Tamam'))],
            ),
          );
          if (mounted) Navigator.of(context).pop(false);
        case PaymentOutcome.pending:
        case PaymentOutcome.cancelled:
          showMessage(context,
              outcome == PaymentOutcome.pending
                  ? 'Ödeme sonucu henüz gelmedi. Mektup detayında güncellenecek.'
                  : 'Ödeme tamamlanmadı. Mektup detayından ödemeye dönebilir veya vazgeçebilirsiniz.');
          Navigator.of(context).pop(false);
      }
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _paying = false);
    }
  }

  void _done(bool free) {
    AppScope.read(context).refreshMe().catchError((_) {});
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (c) => AlertDialog(
        icon: const Icon(Icons.mark_email_read_outlined, size: 48, color: Brand.success),
        title: const Text('Siparişiniz alındı!'),
        content: Text(free
            ? 'Ödemeniz MektupPuan / kampanya indirimiyle tamamlandı. Mektubunuz ilk iş gününde basılıp postaya verilecek.'
            : 'Ödemeniz alındı. Mektubunuz ilk iş gününde basılıp postaya verilecek.'),
        actions: [FilledButton(onPressed: () => Navigator.pop(c), child: const Text('Harika'))],
      ),
    ).then((_) {
      if (mounted) Navigator.of(context).pop(true);
    });
  }

  @override
  Widget build(BuildContext context) {
    final settings = AppScope.of(context).settings!;
    final q = _quote;
    return Scaffold(
      appBar: AppBar(title: const Text('Ödeme')),
      body: q == null
          ? AsyncView<Quote>(future: _future, onRetry: _load, builder: (_, _) => const SizedBox())
          : Stack(children: [
              ListView(padding: const EdgeInsets.fromLTRB(16, 8, 16, 120), children: [
                if (q.providerIsSimulation)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(color: const Color(0xFFFFF1D6), borderRadius: BorderRadius.circular(12)),
                      child: const Row(children: [
                        Icon(Icons.science_outlined, color: Color(0xFF8A5A00)),
                        SizedBox(width: 8),
                        Expanded(child: Text('Test modu: gerçek ödeme alınmaz.', style: TextStyle(color: Color(0xFF8A5A00)))),
                      ]),
                    ),
                  ),
                SectionCard(
                  title: 'Ödeme yöntemi',
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    SegmentedButton<String>(
                      segments: [
                        const ButtonSegment(value: 'Card', icon: Icon(Icons.credit_card), label: Text('Kart')),
                        ButtonSegment(
                            value: 'Mobile',
                            icon: const Icon(Icons.phone_iphone),
                            label: const Text('Mobil ödeme'),
                            enabled: q.mobilePaymentAvailable),
                      ],
                      selected: {_method},
                      onSelectionChanged: (v) {
                        _method = v.first;
                        _load();
                      },
                    ),
                    if (_method == 'Mobile')
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Text(
                          'Mobil ödemede operatör farkı (%${(settings.prices.mobileSurchargeRate * 100).round()}) eklenir.',
                          style: const TextStyle(color: Brand.muted, fontSize: 12)),
                      ),
                  ]),
                ),
                const SizedBox(height: 12),
                if (q.loyaltyEnabled || q.pointsBalance > 0) ...[
                  SectionCard(
                    child: SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      value: _usePoints,
                      onChanged: q.pointsBalance <= 0
                          ? null
                          : (v) {
                              _usePoints = v;
                              _load();
                            },
                      title: const Text('MektupPuan kullan', style: TextStyle(fontWeight: FontWeight.w700)),
                      subtitle: Text('Bakiye: ${number(q.pointsBalance)} puan (${money(q.balanceValue)})'),
                    ),
                  ),
                  const SizedBox(height: 12),
                ],
                SectionCard(
                  title: 'Kupon kodu',
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
                      OutlinedButton(onPressed: _applyCoupon, child: const Text('Uygula')),
                    ]),
                    if (q.couponApplied)
                      const Padding(
                        padding: EdgeInsets.only(top: 8),
                        child: Text('✓ Kupon uygulandı', style: TextStyle(color: Brand.success, fontWeight: FontWeight.w600)),
                      ),
                    if (q.couponMessage != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Text(q.couponMessage!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                      ),
                  ]),
                ),
                const SizedBox(height: 12),
                SectionCard(
                  title: 'Sipariş özeti',
                  child: Column(children: [
                    for (final l in q.lines) InfoRow(l.description, money(l.amount)),
                    if (q.campaignDiscount > 0) ...[
                      const Divider(height: 16),
                      InfoRow('🎁 ${q.campaignTitle ?? 'Kampanya'}', '−${money(q.campaignDiscount)}', valueColor: Brand.success),
                    ],
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
              ]),
              if (_future != null)
                FutureBuilder(
                  future: _future,
                  builder: (_, snap) => snap.connectionState == ConnectionState.done
                      ? const SizedBox.shrink()
                      : const LinearProgressIndicator(minHeight: 2),
                ),
            ]),
      bottomNavigationBar: q == null
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                child: FilledButton.icon(
                  onPressed: _paying ? null : _pay,
                  icon: _paying
                      ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                      : Icon(q.amountToPay == 0 ? Icons.send : Icons.lock_outline),
                  label: Text(q.amountToPay == 0 ? 'Ücretsiz Gönder' : '${money(q.amountToPay)} Öde'),
                ),
              ),
            ),
    );
  }
}
