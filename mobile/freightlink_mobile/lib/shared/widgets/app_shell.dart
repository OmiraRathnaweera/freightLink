import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/loads/screens/admin_loads_screen.dart';
import '../../features/loads/screens/my_loads_screen.dart';
import 'app_top_bar.dart';
import 'coming_soon_placeholder.dart';

/// The app's persistent 4-tab shell (Dashboard / Loads / Payments / Reports)
/// from the mockups. Only the Loads tab is fully built; the rest render a
/// placeholder — see the implementation plan's "App shell" scope decision.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _index = 1; // Loads tab is the app's real home for now.

  @override
  Widget build(BuildContext context) {
    final isAdmin = context.select<AuthProvider, bool>(
      (auth) => auth.user?.isAdmin ?? false,
    );

    final tabs = [
      ComingSoonPlaceholder(
        title: 'Dashboard',
        icon: Icons.dashboard_outlined,
        bottomLeading: const AppAvatar(),
        actions: [
          const NotificationBellButton(),
          if (isAdmin)
            IconButton(
              tooltip: 'All Loads (Admin)',
              icon: const Icon(Icons.admin_panel_settings_outlined),
              onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const AdminLoadsScreen()),
              ),
            ),
        ],
      ),
      const MyLoadsScreen(),
      const ComingSoonPlaceholder(
        title: 'Payments',
        icon: Icons.payments_outlined,
      ),
      const ComingSoonPlaceholder(
        title: 'Reports',
        icon: Icons.bar_chart_rounded,
      ),
    ];

    return Scaffold(
      body: IndexedStack(index: _index, children: tabs),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _index,
        onTap: (value) => setState(() => _index = value),
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.dashboard_outlined),
            label: 'Dashboard',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.local_shipping_outlined),
            label: 'Loads',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.payments_outlined),
            label: 'Payments',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.bar_chart_rounded),
            label: 'Reports',
          ),
        ],
      ),
    );
  }
}
