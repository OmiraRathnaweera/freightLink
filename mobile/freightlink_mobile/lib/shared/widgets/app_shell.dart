import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/loads/screens/my_loads_screen.dart';
import '../../features/notifications/widgets/in_app_push_banner.dart';
import '../../features/trips/screens/driver_assigned_trip_screen.dart';
import '../../features/trips/screens/job_proposals_screen.dart';
import 'app_top_bar.dart';
import 'coming_soon_placeholder.dart';

/// The app's persistent 4-tab shell (Dashboard / Loads, Proposals, or My Trip / Payments / Reports).
/// Renders [DriverAssignedTripScreen] for Drivers, [JobProposalsScreen] for AgencyStaff, and [MyLoadsScreen] for Shippers.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _index = 1; // Loads / Proposals / My Trip tab is the primary operational view.

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final isAgencyStaff = user?.isAgencyStaff ?? false;
    final isDriver = user?.isDriver ?? false;

    final primaryOperationalScreen = isDriver
        ? const DriverAssignedTripScreen()
        : isAgencyStaff
            ? const JobProposalsScreen()
            : const MyLoadsScreen();

    final operationalTabLabel = isDriver
        ? 'My Trip'
        : isAgencyStaff
            ? 'Proposals'
            : 'Loads';

    final operationalTabIcon = isDriver
        ? Icons.navigation_outlined
        : isAgencyStaff
            ? Icons.assignment_outlined
            : Icons.local_shipping_outlined;

    final tabs = [
      ComingSoonPlaceholder(
        title: 'Dashboard',
        icon: Icons.dashboard_outlined,
        bottomLeading: const AppAvatar(),
        actions: const [NotificationBellButton()],
      ),
      primaryOperationalScreen,
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
      body: Stack(
        children: [
          IndexedStack(index: _index, children: tabs),
          const InAppPushBanner(),
        ],
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _index,
        onTap: (value) => setState(() => _index = value),
        items: [
          const BottomNavigationBarItem(
            icon: Icon(Icons.dashboard_outlined),
            label: 'Dashboard',
          ),
          BottomNavigationBarItem(
            icon: Icon(operationalTabIcon),
            label: operationalTabLabel,
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.payments_outlined),
            label: 'Payments',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.bar_chart_rounded),
            label: 'Reports',
          ),
        ],
      ),
    );
  }
}
