import 'package:flutter/material.dart';

import '../../state/session.dart';
import '../../ui/common.dart';
import '../../ui/theme.dart';
import '../tracking_screen.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  bool _obscure = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _error = AppScope.read(context).message;
  }

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await AppScope.read(context).login(_email.text.trim(), _password.text);
    } catch (e) {
      if (mounted) setState(() => _error = errorText(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _form,
                child: AutofillGroup(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    Image.asset('assets/images/logo.png', height: 150),
                    const SizedBox(height: 8),
                    const Text('Sevdiklerinize gerçek mektup gönderin',
                        textAlign: TextAlign.center, style: TextStyle(color: Brand.muted, fontSize: 15)),
                    const SizedBox(height: 28),
                    if (_error != null) ...[
                      _ErrorBox(_error!),
                      const SizedBox(height: 16),
                    ],
                    TextFormField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.email],
                      textInputAction: TextInputAction.next,
                      decoration: const InputDecoration(labelText: 'E-posta', prefixIcon: Icon(Icons.mail_outline)),
                      validator: (v) => (v ?? '').contains('@') ? null : 'Geçerli bir e-posta girin.',
                    ),
                    const SizedBox(height: 14),
                    TextFormField(
                      controller: _password,
                      obscureText: _obscure,
                      autofillHints: const [AutofillHints.password],
                      onFieldSubmitted: (_) => _submit(),
                      decoration: InputDecoration(
                        labelText: 'Şifre',
                        prefixIcon: const Icon(Icons.lock_outline),
                        suffixIcon: IconButton(
                          tooltip: _obscure ? 'Şifreyi göster' : 'Şifreyi gizle',
                          icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                          onPressed: () => setState(() => _obscure = !_obscure),
                        ),
                      ),
                      validator: (v) => (v ?? '').isEmpty ? 'Şifre zorunludur.' : null,
                    ),
                    const SizedBox(height: 22),
                    FilledButton(
                      onPressed: _busy ? null : _submit,
                      child: _busy
                          ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
                          : const Text('Giriş Yap'),
                    ),
                    const SizedBox(height: 12),
                    OutlinedButton(
                      onPressed: _busy
                          ? null
                          : () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const RegisterScreen())),
                      child: const Text('Hesabınız yok mu? Üye olun'),
                    ),
                    const SizedBox(height: 24),
                    TextButton.icon(
                      onPressed: () => Navigator.of(context)
                          .push(MaterialPageRoute(builder: (_) => const TrackingScreen(standalone: true))),
                      icon: const Icon(Icons.local_shipping_outlined),
                      label: const Text('Gönderi takibi'),
                    ),
                  ]),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ErrorBox extends StatelessWidget {
  const _ErrorBox(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    final c = Theme.of(context).colorScheme.error;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: c.withValues(alpha: 0.3)),
      ),
      child: Row(children: [
        Icon(Icons.error_outline, color: c),
        const SizedBox(width: 10),
        Expanded(child: Text(text, style: TextStyle(color: c))),
      ]),
    );
  }
}
