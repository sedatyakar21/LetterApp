import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

class PointsScreen extends StatelessWidget {
  const PointsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final session = AppScope.read(context);
    return Scaffold(
      appBar: AppBar(title: const Text('MektupPuan')),
      body: AsyncView<PointsInfo>(
        future: session.api.points(),
        builder: (context, p) => ListView(padding: const EdgeInsets.all(16), children: [
          BrandHeader(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('Bakiyeniz', style: TextStyle(color: Colors.white70)),
              Text('${number(p.balance)} puan', style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w800)),
              Text('${money(p.balanceValue)} değerinde · 1 puan = ${money(p.pointValue)}',
                  style: const TextStyle(color: Colors.white70)),
              const SizedBox(height: 8),
              Text('Toplam kazanılan: ${number(p.totalEarned)} puan', style: const TextStyle(color: Colors.white70, fontSize: 12)),
            ]),
          ),
          const SizedBox(height: 12),
          SectionCard(
            title: 'Nasıl kazanılır?',
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(p.earnRule ?? 'Puan kazanımı şu an kapalı. Mevcut puanlarınızı kullanmaya devam edebilirsiniz.'),
              const SizedBox(height: 6),
              const Text('Puanlar mektubunuz postaya verildiğinde hesabınıza geçer; ödeme ekranında kullanabilirsiniz.',
                  style: TextStyle(color: Brand.muted, fontSize: 13)),
            ]),
          ),
          const SizedBox(height: 12),
          SectionCard(
            title: 'Arkadaşını davet et',
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text(p.referralRule ?? 'Arkadaşlarınız kayıt olurken referans numaranızı girebilir.'),
              const SizedBox(height: 10),
              Row(children: [
                Expanded(
                  child: Container(
                    padding: const EdgeInsets.symmetric(vertical: 12),
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: Brand.paper2,
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(color: Brand.line),
                    ),
                    child: SelectableText(p.referralCode,
                        style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, letterSpacing: 2, color: Brand.navy)),
                  ),
                ),
                const SizedBox(width: 10),
                IconButton.filledTonal(
                  tooltip: 'Davet bağlantısını kopyala',
                  onPressed: () {
                    Clipboard.setData(ClipboardData(text: '${session.api.client.baseUrl}/kayit?ref=${p.referralCode}'));
                    showMessage(context, 'Davet bağlantınız kopyalandı.');
                  },
                  icon: const Icon(Icons.copy),
                ),
              ]),
              const SizedBox(height: 6),
              Text('Davet ettiğiniz üye: ${p.referredCount}', style: const TextStyle(color: Brand.muted, fontSize: 13)),
            ]),
          ),
          const SizedBox(height: 12),
          SectionCard(
            title: 'Hareketler',
            child: p.transactions.isEmpty
                ? const Text('Henüz puan hareketi yok.', style: TextStyle(color: Brand.muted))
                : Column(children: [
                    for (final t in p.transactions)
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        dense: true,
                        title: Text(t.note.isEmpty ? t.typeLabel : t.note),
                        subtitle: Text('${t.typeLabel} · ${dateTimeText(t.at)}'),
                        trailing: Text(
                          '${t.points >= 0 ? '+' : ''}${number(t.points)}',
                          style: TextStyle(
                              fontWeight: FontWeight.w700,
                              color: t.points >= 0 ? Brand.success : Theme.of(context).colorScheme.error),
                        ),
                      ),
                  ]),
          ),
        ]),
      ),
    );
  }
}
