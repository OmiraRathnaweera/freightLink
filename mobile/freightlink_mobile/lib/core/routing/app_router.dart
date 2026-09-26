import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/auth/screens/login_screen.dart';
import '../../features/auth/screens/register_screen.dart';
import '../../features/loads/screens/my_loads_screen.dart';
import '../../features/trips/screens/driver_assigned_trip_screen.dart';
import '../../features/trips/screens/job_proposals_screen.dart';
import '../../shared/widgets/app_shell.dart';
import '../../shared/widgets/app_top_bar.dart';
import '../../shared/widgets/coming_soon_placeholder.dart';
import '../../features/agencies/screens/agency_dashboard_screen.dart';
import '../../features/agencies/screens/agency_profile_screen.dart';
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
    // The operational tab (Loads/Proposals/My Trip, depending on role) is the
    // primary view post-login, not Dashboard — StatefulShellRoute branches
    // build lazily on first visit, so landing on '/dashboard' would mean the
    // '/loads' branch (and thus MyLoadsScreen/JobProposalsScreen/
    // DriverAssignedTripScreen) is never built until the user taps that tab.
    initialLocation: '/loads',
    refreshListenable: authProvider,
    redirect: (context, state) {
      final status = authProvider.status;
      final isGoingToLogin = state.matchedLocation == '/login';
      final isGoingToRegister = state.matchedLocation == '/register';

      if (status == AuthStatus.unknown) {
        // App is still bootstrapping
        return null;
      }

      if (status == AuthStatus.guest && !isGoingToLogin && !isGoingToRegister) {
        return '/login';
      }

      if (status == AuthStatus.authenticated && (isGoingToLogin || isGoingToRegister)) {
        // Same reasoning as initialLocation above: land on the operational
        // tab, not Dashboard, so it's actually built.
        return '/loads';
      }

      return null;
    },
    routes: [
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/register',
        builder: (context, state) => const RegisterScreen(),
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
                builder: (context, state) {
                  final user = context.watch<AuthProvider>().user;
                  if (user?.isAgencyStaff ?? false) {
                    return const AgencyDashboardScreen();
                  }
                  // Shipper/Driver dashboards aren't built yet.
                  return const ComingSoonPlaceholder(
                    title: 'Dashboard',
                    icon: Icons.dashboard_outlined,
                    bottomLeading: AppAvatar(),
                    actions: [NotificationBellButton()],
                  );
                },
                routes: [
                  GoRoute(
                    path: 'profile',
                    builder: (context, state) => const AgencyProfileScreen(),
                  ),
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
                builder: (context, state) {
                  final user = context.watch<AuthProvider>().user;
                  if (user?.isDriver ?? false) {
                    return const DriverAssignedTripScreen();
                  }
                  if (user?.isAgencyStaff ?? false) {
                    return const JobProposalsScreen();
                  }
                  return const MyLoadsScreen();
                },
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
