import 'package:flutter/material.dart';

import 'account/account_screen.dart';
import 'campaigns/campaigns_screen.dart';
import 'letters/letters_screen.dart';
import 'tracking_screen.dart';

/// Alt menülü ana ekran: Mektuplarım, Kampanyalar, Takip, Hesabım.
class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(index: _index, children: const [
        LettersScreen(),
        CampaignsScreen(),
        TrackingScreen(),
        AccountScreen(),
      ]),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.mail_outline), selectedIcon: Icon(Icons.mail), label: 'Mektuplarım'),
          NavigationDestination(
              icon: Icon(Icons.local_offer_outlined), selectedIcon: Icon(Icons.local_offer), label: 'Kampanyalar'),
          NavigationDestination(
              icon: Icon(Icons.local_shipping_outlined), selectedIcon: Icon(Icons.local_shipping), label: 'Takip'),
          NavigationDestination(icon: Icon(Icons.person_outline), selectedIcon: Icon(Icons.person), label: 'Hesabım'),
        ],
      ),
    );
  }
}
