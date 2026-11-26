import 'package:flutter/material.dart';

import '../../api/models.dart';
import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';

/// Şifremi unuttum. E-posta yönteminde bağlantı e-postayla gelir ve sitede açılır;
/// SMS yönteminde kayıtlı telefona gelen kod ve yeni şifre bu ekranda girilir.
/// Hangi yöntemlerin açık olduğu yönetim panelinden belirlenir.
class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key, this.initialEmail = ''});
  final String initialEmail;

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

enum _Step { request, emailSent, code }

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _requestForm = GlobalKey<FormState>();
  final _codeForm = GlobalKey<FormState>();
  late final _email = TextEditingController(text: widget.initialEmail);
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  PasswordResetOptions? _options;
  String _method = 'Email';
  _Step _step = _Step.request;
  bool _busy = false;
  bool _obscure = true;
  String? _error;
  String? _info;

  @override
  void initState() {
    super.initState();
    _loadOptions();
  }

  @override
  void dispose() {
    _email.dispose();
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _loadOptions() async {
    try {
      final o = await AppScope.read(context).api.passwordResetOptions();
      if (!mounted) return;
      setState(() {
        _options = o;
        _method = o.emailEnabled ? 'Email' : 'Sms';
      });
    } catch (e) {
      if (mounted) setState(() => _error = errorText(e));
    }
  }

  Future<void> _run(Future<void> Function() action) async {
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

  Future<void> _request() async {
    if (!_requestForm.currentState!.validate()) return;
    await _run(() async {
      final message = await AppScope.read(context).api.requestPasswordReset(_email.text.trim(), _method);
      if (!mounted) return;
      setState(() {
        _info = message;
        _step = _method == 'Sms' ? _Step.code : _Step.emailSent;
        _code.clear();
      });
    });
  }

  Future<void> _confirmCode() async {
    if (!_codeForm.currentState!.validate()) return;
    await _run(() async {
      final message = await AppScope.read(context)
          .api
          .confirmPasswordReset(_email.text.trim(), _code.text.trim(), _password.text);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      Navigator.of(context).pop(_email.text.trim());
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Şifremi Unuttum')),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                if (_error != null) ...[
                  _MessageBox(_error!, error: true),
                  const SizedBox(height: 16),
                ],
                if (_options == null && _error == null)
                  const Padding(padding: EdgeInsets.all(24), child: Center(child: CircularProgressIndicator()))
                else if (_options != null && !_options!.anyEnabled)
                  const Text(
                      'Şifre sıfırlama şu an kullanılamıyor. Hesabınız Google\'a bağlıysa web sitesinden Google ile giriş yapabilir '
                      'ya da destek ekibimize yazabilirsiniz.')
                else if (_options != null)
                  switch (_step) {
                    _Step.request => _requestView(),
                    _Step.emailSent => _emailSentView(),
                    _Step.code => _codeView(),
                  },
              ]),
            ),
          ),
        ),
      ),
    );
  }

  Widget _requestView() {
    final o = _options!;
    final both = o.emailEnabled && o.smsEnabled;
    return Form(
      key: _requestForm,
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Text(
          both
              ? 'Üyelikte kullandığınız e-posta adresini girin ve yeni şifreyi nasıl belirlemek istediğinizi seçin.'
              : o.emailEnabled
                  ? 'Üyelikte kullandığınız e-posta adresini girin; size yeni şifre belirleme bağlantısı gönderelim.'
                  : 'Üyelikte kullandığınız e-posta adresini girin; kayıtlı cep telefonunuza doğrulama kodu gönderelim.',
          style: TextStyle(color: Brand.muted),
        ),
        const SizedBox(height: 16),
        TextFormField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.email],
          decoration: const InputDecoration(labelText: 'E-posta', prefixIcon: Icon(Icons.mail_outline)),
          validator: (v) => (v ?? '').contains('@') ? null : 'Geçerli bir e-posta girin.',
        ),
        if (both) ...[
          const SizedBox(height: 12),
          RadioGroup<String>(
            groupValue: _method,
            onChanged: (v) => setState(() => _method = v ?? 'Email'),
            child: const Column(children: [
              RadioListTile<String>(
                value: 'Email',
                contentPadding: EdgeInsets.zero,
                title: Text('E-postama bağlantı gönder'),
                secondary: Icon(Icons.mail_outline),
              ),
              RadioListTile<String>(
                value: 'Sms',
                contentPadding: EdgeInsets.zero,
                title: Text('Kayıtlı telefonuma SMS kodu gönder'),
                secondary: Icon(Icons.sms_outlined),
              ),
            ]),
          ),
        ],
        const SizedBox(height: 20),
        FilledButton(
          onPressed: _busy ? null : _request,
          child: _busy ? const _Spinner() : Text(_method == 'Sms' ? 'Kod Gönder' : 'Bağlantı Gönder'),
        ),
      ]),
    );
  }

  Widget _emailSentView() => Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Icon(Icons.mark_email_read_outlined, size: 56, color: Brand.navy),
        const SizedBox(height: 12),
        _MessageBox(_info ?? ''),
        const SizedBox(height: 12),
        Text(
          'E-postadaki "Yeni Şifre Belirle" bağlantısına dokunun; yeni şifrenizi web sitesinde belirledikten sonra '
          'uygulamaya yeni şifrenizle giriş yapın. E-posta gelmezse gereksiz (spam) klasörüne bakın.',
          style: TextStyle(color: Brand.muted),
        ),
        const SizedBox(height: 20),
        FilledButton(onPressed: () => Navigator.of(context).pop(_email.text.trim()), child: const Text('Girişe Dön')),
        TextButton(
          onPressed: _busy ? null : () => setState(() => _step = _Step.request),
          child: const Text('Tekrar gönder'),
        ),
      ]);

  Widget _codeView() {
    final length = _options!.codeLength;
    return Form(
      key: _codeForm,
      child: AutofillGroup(
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          if (_info != null) ...[_MessageBox(_info!), const SizedBox(height: 16)],
          TextFormField(
            controller: _code,
            keyboardType: TextInputType.number,
            autofillHints: const [AutofillHints.oneTimeCode],
            maxLength: length,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 22, letterSpacing: 6, fontFamily: 'monospace'),
            decoration: const InputDecoration(labelText: 'SMS ile gelen kod', counterText: ''),
            validator: (v) => RegExp('^\\d{$length}\$').hasMatch((v ?? '').trim()) ? null : '$length haneli kodu girin.',
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: _password,
            obscureText: _obscure,
            autofillHints: const [AutofillHints.newPassword],
            decoration: InputDecoration(
              labelText: 'Yeni şifre',
              helperText: 'En az 8 karakter.',
              prefixIcon: const Icon(Icons.lock_outline),
              suffixIcon: IconButton(
                tooltip: _obscure ? 'Şifreyi göster' : 'Şifreyi gizle',
                icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                onPressed: () => setState(() => _obscure = !_obscure),
              ),
            ),
            validator: (v) => (v ?? '').length >= 8 ? null : 'Şifre en az 8 karakter olmalıdır.',
          ),
          const SizedBox(height: 14),
          TextFormField(
            controller: _confirm,
            obscureText: _obscure,
            autofillHints: const [AutofillHints.newPassword],
            decoration: const InputDecoration(labelText: 'Yeni şifre (tekrar)', prefixIcon: Icon(Icons.lock_outline)),
            validator: (v) => v == _password.text ? null : 'Şifreler eşleşmiyor.',
          ),
          const SizedBox(height: 20),
          FilledButton(onPressed: _busy ? null : _confirmCode, child: _busy ? const _Spinner() : const Text('Şifremi Değiştir')),
          TextButton(
            onPressed: _busy ? null : () => setState(() => _step = _Step.request),
            child: const Text('Kod gelmedi mi? Yeniden gönder'),
          ),
        ]),
      ),
    );
  }
}

class _Spinner extends StatelessWidget {
  const _Spinner();
  @override
  Widget build(BuildContext context) =>
      const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white));
}

class _MessageBox extends StatelessWidget {
  const _MessageBox(this.text, {this.error = false});
  final String text;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final c = error ? Theme.of(context).colorScheme.error : Brand.navy;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: c.withValues(alpha: 0.3)),
      ),
      child: Row(children: [
        Icon(error ? Icons.error_outline : Icons.info_outline, color: c),
        const SizedBox(width: 10),
        Expanded(child: Text(text, style: TextStyle(color: c))),
      ]),
    );
  }
}
