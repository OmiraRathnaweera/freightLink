import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/auth/screens/login_screen.dart';
import '../../features/loads/screens/my_loads_screen.dart';
import '../../shared/widgets/app_shell.dart';
import '../../shared/widgets/coming_soon_placeholder.dart';
import '../../features/agencies/screens/agency_dashboard_screen.dart';
import '../../features/agencies/screens/fleet_list_screen.dart';
import '../../features/agencies/screens/add_vehicle_screen.dart';
import '../../features/agencies/screens/driver_onboarding_screen.dart';
import '../../features/agencies/screens/compliance_docs_screen.dart';

final GlobalKey<NavigatorState> _rootNavigatorKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorDashboardKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorLoadsKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorPaymentsKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorReportsKey = GlobalKey<NavigatorState>();

GoRouter createAppRouter(AuthProvider authProvider) {
  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: '/dashboard',
    refreshListenable: authProvider,
    redirect: (context, state) {
      final status = authProvider.status;
      final isGoingToLogin = state.matchedLocation == '/login';

      if (status == AuthStatus.unknown) {
        // App is still bootstrapping
        return null;
      }

      if (status == AuthStatus.guest && !isGoingToLogin) {
        return '/login';
      }

      if (status == AuthStatus.authenticated && isGoingToLogin) {
        return '/dashboard';
      }

      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginScreen(),
      ),
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) {
          return AppShell(navigationShell: navigationShell);
        },
        branches: [
          StatefulShellBranch(
            navigatorKey: _shellNavigatorDashboardKey,
            routes: [
              GoRoute(
                path: '/dashboard',
                builder: (context, state) => const AgencyDashboardScreen(),
                routes: [
                  GoRoute(
                    path: 'fleet',
                    builder: (context, state) => const FleetListScreen(),
                    routes: [
                      GoRoute(
                        path: 'add',
                        builder: (context, state) => const AddVehicleScreen(),
                      ),
                    ],
                  ),
                  GoRoute(
                    path: 'driver-onboarding',
                    builder: (context, state) => const DriverOnboardingScreen(),
                  ),
                  GoRoute(
                    path: 'compliance-docs',
                    builder: (context, state) => const ComplianceDocsScreen(),
                  ),
                ],
              ),
            ],
          ),
          StatefulShellBranch(
            navigatorKey: _shellNavigatorLoadsKey,
            routes: [
              GoRoute(
                path: '/loads',
                builder: (context, state) => const MyLoadsScreen(),
              ),
            ],
          ),
          StatefulShellBranch(
            navigatorKey: _shellNavigatorPaymentsKey,
            routes: [
              GoRoute(
                path: '/payments',
                builder: (context, state) => const ComingSoonPlaceholder(
                  title: 'Payments',
                  icon: Icons.payments_outlined,
                ),
              ),
            ],
          ),
          StatefulShellBranch(
            navigatorKey: _shellNavigatorReportsKey,
            routes: [
              GoRoute(
                path: '/reports',
                builder: (context, state) => const ComingSoonPlaceholder(
                  title: 'Reports',
                  icon: Icons.bar_chart_rounded,
                ),
              ),
            ],
          ),
        ],
      ),
    ],
  );
}
