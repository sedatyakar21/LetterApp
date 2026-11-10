import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';
import 'points_screen.dart';
import 'reviews_screen.dart';

class AccountScreen extends StatelessWidget {
  const AccountScreen({super.key});

  Future<void> _open(BuildContext context, Uri uri) async {
    if (!await launchUrl(uri, mode: LaunchMode.externalApplication) && context.mounted) {
      showMessage(context, 'Bağlantı açılamadı.', error: true);
    }
  }

  Future<void> _setBirthDate(BuildContext context) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 110),
      lastDate: now,
      helpText: 'Doğum tarihiniz',
      initialEntryMode: DatePickerEntryMode.calendarOnly,
    );
    if (picked == null || !context.mounted) return;
    final ok = await confirm(context,
        title: 'Doğum tarihi kaydedilsin mi?',
        message: '${dateText(picked)}\n\nDoğum tarihi bir kez girilir; sonradan yalnızca destek ekibi değiştirebilir.',
        ok: 'Kaydet');
    if (!ok || !context.mounted) return;
    final session = AppScope.read(context);
    try {
      await session.api.setBirthDate(picked);
      await session.refreshMe();
      if (context.mounted) showMessage(context, 'Doğum tarihiniz kaydedildi. 🎂 Doğum günü kampanyalarından yararlanabilirsiniz.');
    } catch (e) {
      if (context.mounted) showMessage(context, errorText(e), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = AppScope.of(context);
    final me = session.me;
    final support = session.settings!.support;
    final base = session.api.client.baseUrl;
    if (me == null) return const Scaffold(body: Center(child: CircularProgressIndicator()));

    return Scaffold(
      appBar: AppBar(title: const Text('Hesabım')),
      body: RefreshIndicator(
        onRefresh: () => session.refreshMe().catchError((_) {}),
        child: ListView(padding: const EdgeInsets.all(16), children: [
          BrandHeader(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(me.fullName, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
              const SizedBox(height: 2),
              Text(me.email, style: const TextStyle(color: Colors.white70)),
              Text(me.phone, style: const TextStyle(color: Colors.white70)),
              const SizedBox(height: 10),
              Text('Üyelik: ${dateText(me.memberSince)}', style: const TextStyle(color: Colors.white70, fontSize: 12)),
            ]),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(children: [
              ListTile(
                leading: const Icon(Icons.stars_outlined, color: Brand.burgundy),
                title: const Text('MektupPuan'),
                subtitle: Text('${number(me.pointsBalance)} puan · ${money(me.pointsBalance * me.pointValue)} değerinde'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PointsScreen())),
              ),
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: const Icon(Icons.card_giftcard_outlined, color: Brand.burgundy),
                title: const Text('Referans numaram'),
                subtitle: Text('${me.referralCode} · ${me.referredCount} arkadaş davet ettiniz'),
                trailing: IconButton(
                  tooltip: 'Kopyala',
                  icon: const Icon(Icons.copy),
                  onPressed: () {
                    Clipboard.setData(ClipboardData(text: '$base/kayit?ref=${me.referralCode}'));
                    showMessage(context, 'Davet bağlantınız kopyalandı. Arkadaşlarınızla paylaşabilirsiniz.');
                  },
                ),
              ),
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: const Icon(Icons.cake_outlined, color: Brand.burgundy),
                title: const Text('Doğum tarihi'),
                subtitle: Text(me.birthDate == null ? 'Girilmedi · doğum günü kampanyaları için ekleyin' : dateText(me.birthDate!)),
                trailing: me.birthDate == null ? const Icon(Icons.add) : null,
                onTap: me.birthDate == null ? () => _setBirthDate(context) : null,
              ),
              if (me.googleLinked) ...[
                const Divider(height: 1, indent: 16, endIndent: 16),
                const ListTile(
                  leading: Icon(Icons.verified_user_outlined, color: Brand.success),
                  title: Text('Google hesabı bağlı'),
                  subtitle: Text('Bağlantıyı web sitesindeki Hesabım sayfasından yönetebilirsiniz.'),
                ),
              ],
            ]),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(children: [
              ListTile(
                leading: const Icon(Icons.reviews_outlined, color: Brand.navy),
                title: const Text('Müşteri yorumları'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ReviewsScreen())),
              ),
              if (support.whatsAppNumber != null) ...[
                const Divider(height: 1, indent: 16, endIndent: 16),
                ListTile(
                  leading: const Icon(Icons.chat_outlined, color: Color(0xFF25D366)),
                  title: const Text('WhatsApp destek'),
                  subtitle: Text([support.whatsAppDisplay, support.hours].whereType<String>().join(' · ')),
                  onTap: () => _open(context, Uri.parse(
                      'https://wa.me/${support.whatsAppNumber}?text=${Uri.encodeComponent('Merhaba, BiMektup uygulaması hakkında yardım almak istiyorum.')}')),
                ),
              ],
              if (support.email != null) ...[
                const Divider(height: 1, indent: 16, endIndent: 16),
                ListTile(
                  leading: const Icon(Icons.mail_outline, color: Brand.navy),
                  title: const Text('E-posta'),
                  subtitle: Text(support.email!),
                  onTap: () => _open(context, Uri(scheme: 'mailto', path: support.email)),
                ),
              ],
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: const Icon(Icons.help_outline, color: Brand.navy),
                title: const Text('Sıkça sorulan sorular'),
                trailing: const Icon(Icons.open_in_new, size: 18),
                onTap: () => _open(context, Uri.parse('$base/sss')),
              ),
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: const Icon(Icons.description_outlined, color: Brand.navy),
                title: const Text('Üyelik sözleşmesi'),
                trailing: const Icon(Icons.open_in_new, size: 18),
                onTap: () => _open(context, Uri.parse('$base/uyelik-sozlesmesi')),
              ),
            ]),
          ),
          const SizedBox(height: 20),
          OutlinedButton.icon(
            style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
            onPressed: () async {
              if (await confirm(context, title: 'Çıkış yapılsın mı?', message: 'Tekrar giriş yapmanız gerekecek.', ok: 'Çıkış yap')) {
                await session.logout();
              }
            },
            icon: const Icon(Icons.logout),
            label: const Text('Çıkış yap'),
          ),
        ]),
      ),
    );
  }
}
