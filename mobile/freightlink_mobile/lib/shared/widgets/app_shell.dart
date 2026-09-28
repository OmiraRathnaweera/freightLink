import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/notifications/widgets/in_app_push_banner.dart';

/// The app's persistent bottom-nav shell, driven by go_router's
/// [StatefulShellRoute] (see `core/routing/app_router.dart`): each branch
/// owns its own navigation stack, and this widget only renders whichever
/// branch is active (`navigationShell`) plus the bottom nav bar around it.
///
/// The 2nd tab's icon/label is role-dependent (Driver: "My Trip", AgencyStaff:
/// "Proposals", Shipper: "Loads") since each role sees a different screen at
/// that branch's route — chosen by the branch's own `GoRoute.builder` in
/// app_router.dart, not here; this widget only needs to know the label/icon.
class AppShell extends StatelessWidget {
  const AppShell({
    super.key,
    required this.navigationShell,
  });

  final StatefulNavigationShell navigationShell;

  void _onTap(BuildContext context, int index) {
    navigationShell.goBranch(
      index,
      initialLocation: index == navigationShell.currentIndex,
    );
  }

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final isAgencyStaff = user?.isAgencyStaff ?? false;
    final isDriver = user?.isDriver ?? false;

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

    return Scaffold(
      body: Stack(
        children: [
          navigationShell,
          const InAppPushBanner(),
        ],
      ),
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: navigationShell.currentIndex,
        onTap: (index) => _onTap(context, index),
        type: BottomNavigationBarType.fixed,
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
        ],
      ),
    );
  }
}
