import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../api/models.dart';
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

  /// E-posta / telefon satırı: onay bekleyen değişiklik varsa altında belirtilir; dokununca değiştirme penceresi açılır.
  Widget _contactTile(BuildContext context, Me me,
      {required String kind, required IconData icon, required String title, required String value}) {
    final pending = me.pending(kind == 'email' ? 'Email' : 'Phone');
    return ListTile(
      leading: Icon(icon, color: Brand.navy),
      title: Text(title),
      subtitle: Text(pending == null ? value : '$value\nOnay bekliyor: ${pending.destination}'),
      isThreeLine: pending != null,
      trailing: Text(pending == null ? 'Değiştir' : 'Kodu gir', style: TextStyle(color: Brand.burgundy, fontWeight: FontWeight.w600)),
      onTap: () => showDialog<void>(context: context, builder: (_) => ContactChangeDialog(me: me, kind: kind)),
    );
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
              _contactTile(context, me, kind: 'email', icon: Icons.mail_outline, title: 'E-posta', value: me.email),
              const Divider(height: 1, indent: 16, endIndent: 16),
              _contactTile(context, me, kind: 'phone', icon: Icons.phone_iphone, title: 'Cep telefonu', value: me.phone),
            ]),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(children: [
              ListTile(
                leading: Icon(Icons.stars_outlined, color: Brand.burgundy),
                title: const Text('MektupPuan'),
                subtitle: Text('${number(me.pointsBalance)} puan · ${money(me.pointsBalance * me.pointValue)} değerinde'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PointsScreen())),
              ),
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: Icon(Icons.card_giftcard_outlined, color: Brand.burgundy),
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
                leading: Icon(Icons.cake_outlined, color: Brand.burgundy),
                title: const Text('Doğum tarihi'),
                subtitle: Text(me.birthDate == null ? 'Girilmedi · doğum günü kampanyaları için ekleyin' : dateText(me.birthDate!)),
                trailing: me.birthDate == null ? const Icon(Icons.add) : null,
                onTap: me.birthDate == null ? () => _setBirthDate(context) : null,
              ),
              if (me.googleLinked) ...[
                const Divider(height: 1, indent: 16, endIndent: 16),
                ListTile(
                  leading: Icon(Icons.verified_user_outlined, color: Brand.success),
                  title: Text('Google hesabı bağlı'),
                  subtitle: Text('Bağlantıyı web sitesindeki Hesabım sayfasından yönetebilirsiniz.'),
                ),
              ],
            ]),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 14),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                Row(children: [
                  Icon(Icons.dark_mode_outlined, color: Brand.navy),
                  const SizedBox(width: 12),
                  const Text('Görünüm', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 16)),
                ]),
                const SizedBox(height: 10),
                SegmentedButton<ThemeMode>(
                  showSelectedIcon: false,
                  segments: const [
                    ButtonSegment(value: ThemeMode.system, icon: Icon(Icons.brightness_auto_outlined), label: Text('Sistem')),
                    ButtonSegment(value: ThemeMode.light, icon: Icon(Icons.light_mode_outlined), label: Text('Açık')),
                    ButtonSegment(value: ThemeMode.dark, icon: Icon(Icons.dark_mode_outlined), label: Text('Koyu')),
                  ],
                  selected: {session.themeMode},
                  onSelectionChanged: (v) => session.setThemeMode(v.first),
                ),
                if (session.themeMode == ThemeMode.system)
                  Padding(
                    padding: const EdgeInsets.only(top: 6),
                    child: Text('Telefonunuzun açık / koyu ayarına göre değişir.', style: TextStyle(color: Brand.muted, fontSize: 12)),
                  ),
              ]),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Column(children: [
              ListTile(
                leading: Icon(Icons.reviews_outlined, color: Brand.navy),
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
                      'https://wa.me/${support.whatsAppNumber}?text=${Uri.encodeComponent('Merhaba, BiMektubum uygulaması hakkında yardım almak istiyorum.')}')),
                ),
              ],
              if (support.email != null) ...[
                const Divider(height: 1, indent: 16, endIndent: 16),
                ListTile(
                  leading: Icon(Icons.mail_outline, color: Brand.navy),
                  title: const Text('E-posta'),
                  subtitle: Text(support.email!),
                  onTap: () => _open(context, Uri(scheme: 'mailto', path: support.email)),
                ),
              ],
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: Icon(Icons.help_outline, color: Brand.navy),
                title: const Text('Sıkça sorulan sorular'),
                trailing: const Icon(Icons.open_in_new, size: 18),
                onTap: () => _open(context, Uri.parse('$base/sss')),
              ),
              const Divider(height: 1, indent: 16, endIndent: 16),
              ListTile(
                leading: Icon(Icons.description_outlined, color: Brand.navy),
                title: const Text('Sözleşmeler ve yasal metinler'),
                trailing: const Icon(Icons.open_in_new, size: 18),
                onTap: () => _open(context, Uri.parse('$base/sozlesmeler')),
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

/// E-posta veya cep telefonu değişikliği: 1) yeni değer (+ şifreli hesaplarda mevcut şifre) → onay kodu gönderilir,
/// 2) yeni adrese gelen 6 haneli kod girilir → değişiklik uygulanır.
class ContactChangeDialog extends StatefulWidget {
  const ContactChangeDialog({super.key, required this.me, required this.kind});
  final Me me;

  /// 'email' / 'phone'
  final String kind;

  @override
  State<ContactChangeDialog> createState() => _ContactChangeDialogState();
}

class _ContactChangeDialogState extends State<ContactChangeDialog> {
  final _form = GlobalKey<FormState>();
  final _value = TextEditingController();
  final _password = TextEditingController();
  final _code = TextEditingController();
  PendingContactChange? _pending;
  bool _busy = false;
  String? _error;

  bool get _isEmail => widget.kind == 'email';

  @override
  void initState() {
    super.initState();
    _pending = widget.me.pending(_isEmail ? 'Email' : 'Phone');
  }

  @override
  void dispose() {
    _value.dispose();
    _password.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _run(Future<void> Function() action) async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _error = errorText(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _sendCode() => _run(() async {
        final api = AppScope.read(context).api;
        final password = widget.me.hasPassword ? _password.text : null;
        final p = _isEmail
            ? await api.requestEmailChange(_value.text.trim(), password)
            : await api.requestPhoneChange(_value.text.trim(), password);
        if (!mounted) return;
        setState(() {
          _pending = p;
          _code.clear();
        });
        AppScope.read(context).refreshMe().catchError((_) {});
      });

  Future<void> _confirm() => _run(() async {
        final session = AppScope.read(context);
        await session.api.confirmContactChange(widget.kind, _code.text.trim());
        await session.refreshMe();
        if (!mounted) return;
        Navigator.of(context).pop();
        showMessage(context, _isEmail ? 'E-posta adresiniz güncellendi.' : 'Cep telefonunuz güncellendi.');
      });

  Future<void> _cancel() async {
    final session = AppScope.read(context);
    try {
      await session.api.cancelContactChange(widget.kind);
      await session.refreshMe();
    } catch (_) {}
    if (mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final p = _pending;
    final title = _isEmail ? 'E-posta değiştir' : 'Cep telefonu değiştir';
    return AlertDialog(
      title: Text(title),
      content: SingleChildScrollView(
        child: Form(
          key: _form,
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            if (_error != null) ...[
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 10),
            ],
            if (p == null) ...[
              Text('Mevcut: ${_isEmail ? widget.me.email : widget.me.phone}', style: TextStyle(color: Brand.muted)),
              const SizedBox(height: 10),
              TextFormField(
                controller: _value,
                keyboardType: _isEmail ? TextInputType.emailAddress : TextInputType.phone,
                autofillHints: [_isEmail ? AutofillHints.email : AutofillHints.telephoneNumber],
                decoration: InputDecoration(
                  labelText: _isEmail ? 'Yeni e-posta' : 'Yeni cep telefonu',
                  hintText: _isEmail ? null : '05XX XXX XX XX',
                  prefixIcon: Icon(_isEmail ? Icons.mail_outline : Icons.phone_iphone),
                ),
                validator: (v) => _isEmail
                    ? (v == null || !v.contains('@') ? 'Geçerli bir e-posta girin.' : null)
                    : ((v ?? '').replaceAll(RegExp(r'\D'), '').length < 10 ? 'Geçerli bir cep telefonu girin.' : null),
              ),
              if (widget.me.hasPassword) ...[
                const SizedBox(height: 10),
                TextFormField(
                  controller: _password,
                  obscureText: true,
                  autofillHints: const [AutofillHints.password],
                  decoration: const InputDecoration(labelText: 'Mevcut şifreniz', prefixIcon: Icon(Icons.lock_outline)),
                  validator: (v) => (v ?? '').isEmpty ? 'Güvenliğiniz için şifrenizi girin.' : null,
                ),
              ],
              const SizedBox(height: 8),
              Text(_isEmail ? 'Yeni adrese 6 haneli onay kodu gönderilir.' : 'Yeni numaraya SMS ile 6 haneli onay kodu gönderilir.',
                  style: TextStyle(color: Brand.muted, fontSize: 12)),
            ] else ...[
              Text('Onay kodu ${p.destination} ${_isEmail ? 'adresine e-postayla' : 'numarasına SMS ile'} gönderildi. '
                  'Kod ${p.expiresAt.hour.toString().padLeft(2, '0')}:${p.expiresAt.minute.toString().padLeft(2, '0')}\'e kadar geçerli.'),
              const SizedBox(height: 12),
              TextFormField(
                controller: _code,
                keyboardType: TextInputType.number,
                autofillHints: const [AutofillHints.oneTimeCode],
                maxLength: 6,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 22, letterSpacing: 6, fontWeight: FontWeight.w700),
                decoration: const InputDecoration(labelText: 'Onay kodu', counterText: ''),
                validator: (v) => (v ?? '').replaceAll(RegExp(r'\D'), '').length != 6 ? '6 haneli kodu girin.' : null,
              ),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton(
                  onPressed: _busy ? null : () => setState(() => _pending = null),
                  child: Text(_isEmail ? 'Kodu tekrar gönder / farklı adres' : 'Kodu tekrar gönder / farklı numara'),
                ),
              ),
            ],
          ]),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : (p == null ? () => Navigator.of(context).pop() : _cancel),
          child: Text(p == null ? 'Kapat' : 'Vazgeç'),
        ),
        FilledButton(
          onPressed: _busy ? null : (p == null ? _sendCode : _confirm),
          child: _busy
              ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
              : Text(p == null ? 'Kod Gönder' : 'Onayla'),
        ),
      ],
    );
  }
}
