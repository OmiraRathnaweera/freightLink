import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../features/auth/providers/auth_provider.dart';
import '../../features/auth/screens/login_screen.dart';
import '../../features/loads/screens/my_loads_screen.dart';
import '../../features/loads/screens/shipper_dashboard_screen.dart';
import '../../features/trips/screens/driver_dashboard_screen.dart';
import '../../features/trips/screens/driver_assigned_trip_screen.dart';
import '../../features/trips/screens/job_proposals_screen.dart';
import '../../features/trips/screens/agency_trips_screen.dart';
import '../../features/trips/screens/agency_trip_detail_screen.dart';
import '../../shared/widgets/app_shell.dart';
import '../../shared/widgets/app_top_bar.dart';
import '../../shared/widgets/coming_soon_placeholder.dart';
import '../../features/agencies/screens/agency_dashboard_screen.dart';
import '../../features/agencies/screens/agency_profile_screen.dart';
import '../../features/agencies/screens/fleet_list_screen.dart';
import '../../features/agencies/screens/add_vehicle_screen.dart';
import '../../features/agencies/screens/driver_onboarding_screen.dart';
import '../../features/agencies/screens/add_driver_screen.dart';
import '../../features/agencies/screens/compliance_docs_screen.dart';
import '../../features/agencies/screens/add_compliance_doc_screen.dart';
import '../../features/billing/screens/payments_screen.dart';
import '../../features/billing/screens/raise_dispute_screen.dart';
import '../../features/billing/screens/invoice_detail_screen.dart';
import '../../features/billing/screens/create_invoice_screen.dart';

final GlobalKey<NavigatorState> _rootNavigatorKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorDashboardKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorLoadsKey = GlobalKey<NavigatorState>();
final GlobalKey<NavigatorState> _shellNavigatorPaymentsKey = GlobalKey<NavigatorState>();

// One routing policy for every authenticated mobile role. Screen builders may
// still select role-specific content for shared tab slots, but they must never
// be the only authorization boundary: a user can type a nested URL directly.
const _roleHomes = <String, String>{
  'Shipper': '/loads',
  'AgencyStaff': '/dashboard',
  'Driver': '/loads',
};

const _roleAllowedExactPaths = <String, List<String>>{
  'Shipper': ['/loads', '/payments', '/dashboard'],
  'AgencyStaff': ['/dashboard', '/loads', '/trips', '/payments'],
  'Driver': ['/loads', '/dashboard'],
};

const _roleAllowedNestedPrefixes = <String, List<String>>{
  'Shipper': ['/payments/'],
  'AgencyStaff': [
    '/dashboard/profile',
    '/dashboard/fleet',
    '/dashboard/driver-onboarding',
    '/dashboard/compliance-docs',
    '/payments/',
    '/trips/',
  ],
  'Driver': [],
};

String mobileRoleHome(String? role) => _roleHomes[role] ?? '/login';

bool isMobileRouteAllowed(String? role, String path) =>
    (_roleAllowedExactPaths[role] ?? const <String>[]).contains(path) ||
    (_roleAllowedNestedPrefixes[role] ?? const <String>[]).any(path.startsWith);

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

      if (status == AuthStatus.unknown) {
        // Fail closed while secure storage/session validation is still running.
        // Returning null here exposed protected shell routes briefly (and on a
        // failed bootstrap) to direct URL navigation such as /dashboard.
        return isGoingToLogin ? null : '/login';
      }

      if (status == AuthStatus.guest && !isGoingToLogin) {
        return '/login';
      }

      if (status == AuthStatus.authenticated && isGoingToLogin) {
        return mobileRoleHome(authProvider.user?.role);
      }

      if (status == AuthStatus.authenticated &&
          !isMobileRouteAllowed(authProvider.user?.role, state.matchedLocation)) {
        return mobileRoleHome(authProvider.user?.role);
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
                builder: (context, state) {
                  final user = context.watch<AuthProvider>().user;
                  if (user?.isAgencyStaff ?? false) {
                    return const AgencyDashboardScreen();
                  }
                  if (user?.isShipper ?? false) {
                    return const ShipperDashboardScreen();
                  }
                  if (user?.isDriver ?? false) {
                    return const DriverDashboardScreen();
                  }
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
                    builder: (context, state) => const DriverListScreen(),
                    routes: [
                      GoRoute(
                        path: 'add',
                        builder: (context, state) => const AddDriverScreen(),
                      ),
                    ],
                  ),
                  GoRoute(
                    path: 'compliance-docs',
                    builder: (context, state) => const ComplianceDocsScreen(),
                    routes: [
                      GoRoute(
                        path: 'add',
                        builder: (context, state) => const AddComplianceDocScreen(),
                      ),
                    ],
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
                builder: (context, state) => const PaymentsScreen(),
                routes: [
                  GoRoute(
                    path: 'dispute',
                    builder: (context, state) {
                      final tripId = state.extra as String?;
                      if (tripId == null) {
                        return const Scaffold(
                          body: Center(child: Text('No trip was selected for this dispute.')),
                        );
                      }
                      return RaiseDisputeScreen(tripId: tripId);
                    },
                  ),
                  GoRoute(
                    path: 'create',
                    builder: (context, state) {
                      final tripId = state.extra as String?;
                      return CreateInvoiceScreen(tripId: tripId);
                    },
                  ),
                  GoRoute(
                    path: ':invoiceId',
                    builder: (context, state) {
                      final invoiceId = state.pathParameters['invoiceId']!;
                      return InvoiceDetailScreen(invoiceId: invoiceId);
                    },
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
      GoRoute(
        path: '/trips',
        builder: (context, state) => const AgencyTripsScreen(),
        routes: [
          GoRoute(
            path: ':tripId',
            builder: (context, state) {
              final tripId = state.pathParameters['tripId']!;
              return AgencyTripDetailScreen(tripId: tripId);
            },
          ),
        ],
      ),
    ],
  );
}
