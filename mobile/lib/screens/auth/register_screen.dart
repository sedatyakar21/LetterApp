import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _password2 = TextEditingController();
  final _referral = TextEditingController();
  String? _channel;
  DateTime? _birthDate;
  bool _accept = false;
  bool _busy = false;

  @override
  void dispose() {
    for (final c in [_name, _email, _phone, _password, _password2, _referral]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    if (!_accept) {
      showMessage(context, 'Üyelik sözleşmesini onaylamalısınız.', error: true);
      return;
    }
    setState(() => _busy = true);
    final session = AppScope.read(context);
    try {
      await session.api.register(
        fullName: _name.text.trim(),
        email: _email.text.trim(),
        phone: _phone.text.trim(),
        password: _password.text,
        channel: _channel,
        referralCode: _channel == 'Friend' ? _referral.text.trim() : null,
        birthDate: _birthDate,
        acceptTerms: _accept,
      );
      await session.afterRegister();
      if (mounted) Navigator.of(context).popUntil((r) => r.isFirst);
    } catch (e) {
      if (mounted) showMessage(context, errorText(e), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _pickBirthDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _birthDate ?? DateTime(now.year - 25, now.month, now.day),
      firstDate: DateTime(now.year - 110),
      lastDate: now,
      helpText: 'Doğum tarihiniz',
      initialEntryMode: DatePickerEntryMode.calendarOnly,
    );
    if (picked != null) setState(() => _birthDate = picked);
  }

  @override
  Widget build(BuildContext context) {
    final settings = AppScope.of(context).settings!;
    return Scaffold(
      appBar: AppBar(title: const Text('Üye Ol')),
      body: SafeArea(
        child: Form(
          key: _form,
          child: ListView(padding: const EdgeInsets.all(20), children: [
            TextFormField(
              controller: _name,
              textCapitalization: TextCapitalization.words,
              autofillHints: const [AutofillHints.name],
              decoration: const InputDecoration(labelText: 'Ad Soyad'),
              validator: (v) => (v ?? '').trim().length < 3 ? 'Ad soyad zorunludur.' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.email],
              decoration: const InputDecoration(labelText: 'E-posta'),
              validator: (v) => (v ?? '').contains('@') ? null : 'Geçerli bir e-posta girin.',
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              autofillHints: const [AutofillHints.telephoneNumber],
              decoration: const InputDecoration(labelText: 'Cep telefonu', hintText: '05XX XXX XX XX'),
              validator: (v) => (v ?? '').replaceAll(RegExp(r'\D'), '').length < 10 ? 'Geçerli bir cep telefonu girin.' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _password,
              obscureText: true,
              autofillHints: const [AutofillHints.newPassword],
              decoration: InputDecoration(labelText: 'Şifre', helperText: 'En az ${settings.minPasswordLength} karakter'),
              validator: (v) =>
                  (v ?? '').length < settings.minPasswordLength ? 'Şifre en az ${settings.minPasswordLength} karakter olmalıdır.' : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _password2,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Şifre (tekrar)'),
              validator: (v) => v != _password.text ? 'Şifreler eşleşmiyor.' : null,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: _channel,
              decoration: const InputDecoration(labelText: 'Bizi nereden duydunuz?'),
              items: [for (final o in settings.acquisitionChannels) DropdownMenuItem(value: o.value, child: Text(o.label))],
              onChanged: (v) => setState(() => _channel = v),
              validator: (v) => v == null ? 'Lütfen bir seçenek seçin.' : null,
            ),
            if (_channel == 'Friend') ...[
              const SizedBox(height: 12),
              TextFormField(
                controller: _referral,
                textCapitalization: TextCapitalization.characters,
                decoration: const InputDecoration(
                    labelText: 'Arkadaşınızın referans no\'su (isteğe bağlı)', hintText: 'Ör. AY482915'),
              ),
            ],
            const SizedBox(height: 12),
            InkWell(
              borderRadius: BorderRadius.circular(12),
              onTap: _pickBirthDate,
              child: InputDecorator(
                decoration: InputDecoration(
                  labelText: 'Doğum tarihi (isteğe bağlı)',
                  helperText: 'Doğum günü kampanyalarından yararlanmak için',
                  suffixIcon: _birthDate == null
                      ? const Icon(Icons.cake_outlined)
                      : IconButton(
                          tooltip: 'Temizle',
                          icon: const Icon(Icons.clear),
                          onPressed: () => setState(() => _birthDate = null)),
                ),
                child: Text(_birthDate == null ? 'Seçin' : dateText(_birthDate!),
                    style: TextStyle(color: _birthDate == null ? Brand.muted : Brand.ink)),
              ),
            ),
            const SizedBox(height: 8),
            CheckboxListTile(
              value: _accept,
              onChanged: (v) => setState(() => _accept = v ?? false),
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              title: Wrap(children: [
                InkWell(
                  onTap: () => launchUrl(Uri.parse('${AppScope.read(context).api.client.baseUrl}/uyelik-sozlesmesi'),
                      mode: LaunchMode.externalApplication),
                  child: const Text('Üyelik sözleşmesini',
                      style: TextStyle(color: Brand.burgundy, decoration: TextDecoration.underline)),
                ),
                const Text(' okudum, onaylıyorum.'),
              ]),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                  : const Text('Üye Ol'),
            ),
          ]),
        ),
      ),
    );
  }
}
