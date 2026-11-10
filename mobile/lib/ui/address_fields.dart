import 'package:flutter/material.dart';

import '../api/models.dart';

/// Adres alanlarının denetleyicileri (adres, ilçe, il, posta kodu).
class AddressControllers {
  AddressControllers([Address a = const Address()])
      : line = TextEditingController(text: a.line),
        district = TextEditingController(text: a.district),
        city = TextEditingController(text: a.city),
        postalCode = TextEditingController(text: a.postalCode);

  final TextEditingController line, district, city, postalCode;

  Address get value => Address(
      line: line.text.trim(), district: district.text.trim(), city: city.text.trim(), postalCode: postalCode.text.trim());

  void addListener(VoidCallback l) {
    for (final c in [line, district, city, postalCode]) {
      c.addListener(l);
    }
  }

  void dispose() {
    for (final c in [line, district, city, postalCode]) {
      c.dispose();
    }
  }
}

/// Web formundaki kurallarla aynı doğrulamalı adres alanları.
class AddressFields extends StatelessWidget {
  const AddressFields({super.key, required this.controllers});
  final AddressControllers controllers;

  static String? _required(String? v, String name) => (v ?? '').trim().isEmpty ? '$name zorunludur.' : null;

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      TextFormField(
        controller: controllers.line,
        minLines: 2,
        maxLines: 3,
        maxLength: 250,
        textCapitalization: TextCapitalization.sentences,
        autofillHints: const [AutofillHints.streetAddressLevel1],
        decoration: const InputDecoration(labelText: 'Adres', hintText: 'Mahalle, cadde/sokak, bina ve daire no', counterText: ''),
        validator: (v) => _required(v, 'Adres'),
      ),
      const SizedBox(height: 12),
      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Expanded(
          child: TextFormField(
            controller: controllers.district,
            maxLength: 60,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'İlçe', counterText: ''),
            validator: (v) => _required(v, 'İlçe'),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: TextFormField(
            controller: controllers.city,
            maxLength: 60,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'İl', counterText: ''),
            validator: (v) => _required(v, 'İl'),
          ),
        ),
      ]),
      const SizedBox(height: 12),
      TextFormField(
        controller: controllers.postalCode,
        keyboardType: TextInputType.number,
        maxLength: 5,
        autofillHints: const [AutofillHints.postalCode],
        decoration: const InputDecoration(labelText: 'Posta kodu', counterText: ''),
        validator: (v) => RegExp(r'^\d{5}$').hasMatch((v ?? '').trim()) ? null : 'Posta kodu 5 haneli olmalıdır.',
      ),
    ]);
  }
}
