import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/core/routing/app_router.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/loads/screens/my_loads_screen.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/models/job_proposal.dart';
import 'package:freightlink_mobile/features/trips/screens/driver_assigned_trip_screen.dart';
import 'package:freightlink_mobile/features/trips/screens/job_proposals_screen.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/fixtures.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  setUpAll(registerFallbackValues);

  group('Cross-Platform Auth & Session Consistency (Y3S01-103)', () {
    late MockTokenStorage tokenStorage;
    late MockLoadsRepository mockLoadsRepo;
    late MockTripsRepository mockTripsRepo;

    setUp(() {
      tokenStorage = MockTokenStorage();
      when(() => tokenStorage.readRefreshToken()).thenAnswer((_) async => null);
      when(() => tokenStorage.saveTokenPair(accessToken: any(named: 'accessToken'), refreshToken: any(named: 'refreshToken'))).thenAnswer((_) async {});
      when(() => tokenStorage.clear()).thenAnswer((_) async {});
      mockLoadsRepo = MockLoadsRepository();
      mockTripsRepo = MockTripsRepository();

      when(
        () => mockLoadsRepo.getList(
          search: any(named: 'search'),
          status: any(named: 'status'),
        ),
      ).thenAnswer((_) async => buildPagedResult(const []));

      when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => null);
      when(
        () => mockTripsRepo.getProposals(
          page: any(named: 'page'),
          pageSize: any(named: 'pageSize'),
          status: any(named: 'status'),
          search: any(named: 'search'),
        ),
      ).thenAnswer(
        (_) async => const PagedResult<JobProposal>(
          items: [],
          page: 1,
          pageSize: 10,
          totalItems: 0,
          totalPages: 0,
        ),
      );
    });

    Widget createTestAppShell(AuthUser user) {
      final authProvider = MockAuthProvider();
      when(() => authProvider.user).thenReturn(user);
      when(() => authProvider.status).thenReturn(AuthStatus.authenticated);

      return MultiProvider(
        providers: [
          ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
          ChangeNotifierProvider<NotificationProvider>(
            create: (_) => NotificationProvider(),
          ),
          Provider<LoadsRepository>.value(value: mockLoadsRepo),
          Provider<TripsRepository>.value(value: mockTripsRepo),
        ],
        // AppShell no longer picks the role-specific screen itself — that's
        // now decided by the '/loads' branch's GoRoute.builder in
        // app_router.dart (StatefulShellRoute.indexedStack builds every
        // branch up front), so this must go through the real router rather
        // than constructing AppShell directly.
        child: MaterialApp.router(
          theme: AppTheme.light,
          routerConfig: createAppRouter(authProvider),
        ),
      );
    }

    test('1. AuthUser JSON deserialization matches backend CurrentUserResponseDto contract', () {
      final backendDtoJson = {
        'userId': 'c1000000-0000-0000-0000-000000000001',
        'email': 'driver.user@freightlink.lk',
        'fullName': 'Ruwan Perera',
        'phoneE164': '+94771234567',
        'role': 'Driver',
        'isActive': true,
        'createdAt': '2026-09-01T10:00:00+00:00',
      };

      final authUser = AuthUser.fromJson(backendDtoJson);

      expect(authUser.userId, 'c1000000-0000-0000-0000-000000000001');
      expect(authUser.email, 'driver.user@freightlink.lk');
      expect(authUser.fullName, 'Ruwan Perera');
      expect(authUser.role, 'Driver');
      expect(authUser.isActive, isTrue);
      expect(authUser.isDriver, isTrue);
      expect(authUser.isShipper, isFalse);
      expect(authUser.isAgencyStaff, isFalse);
      expect(authUser.isAdmin, isFalse);
    });

    test('2. Role getters evaluate correctly for all four system roles', () {
      const shipper = AuthUser(
        userId: 'u1',
        email: 's@test.com',
        fullName: 'Shipper One',
        role: 'Shipper',
        isActive: true,
      );
      expect(shipper.isShipper, isTrue);
      expect(shipper.isAgencyStaff, isFalse);
      expect(shipper.isDriver, isFalse);
      expect(shipper.isAdmin, isFalse);

      const agency = AuthUser(
        userId: 'u2',
        email: 'a@test.com',
        fullName: 'Agency One',
        role: 'AgencyStaff',
        isActive: true,
      );
      expect(agency.isShipper, isFalse);
      expect(agency.isAgencyStaff, isTrue);
      expect(agency.isDriver, isFalse);
      expect(agency.isAdmin, isFalse);

      const driver = AuthUser(
        userId: 'u3',
        email: 'd@test.com',
        fullName: 'Driver One',
        role: 'Driver',
        isActive: true,
      );
      expect(driver.isShipper, isFalse);
      expect(driver.isAgencyStaff, isFalse);
      expect(driver.isDriver, isTrue);
      expect(driver.isAdmin, isFalse);

      const admin = AuthUser(
        userId: 'u4',
        email: 'admin@test.com',
        fullName: 'Admin One',
        role: 'Admin',
        isActive: true,
      );
      expect(admin.isShipper, isFalse);
      expect(admin.isAgencyStaff, isFalse);
      expect(admin.isDriver, isFalse);
      expect(admin.isAdmin, isTrue);
    });

    testWidgets('3. AppShell maps Shipper role to MyLoadsScreen and Loads tab', (tester) async {
      const shipper = AuthUser(
        userId: 'u-ship',
        email: 'shipper@test.com',
        fullName: 'Shipper User',
        role: 'Shipper',
        isActive: true,
      );

      await tester.pumpWidget(createTestAppShell(shipper));
      await tester.pumpAndSettle();

      expect(find.byType(MyLoadsScreen), findsOneWidget);
      expect(find.text('Loads'), findsOneWidget);
      expect(find.text('Payments'), findsOneWidget);
      expect(find.byType(JobProposalsScreen), findsNothing);
      expect(find.byType(DriverAssignedTripScreen), findsNothing);
    });

    testWidgets('4. AppShell maps AgencyStaff role to JobProposalsScreen and Proposals tab', (tester) async {
      const agency = AuthUser(
        userId: 'u-agency',
        email: 'agency@test.com',
        fullName: 'Agency Staff',
        role: 'AgencyStaff',
        isActive: true,
      );

      await tester.pumpWidget(createTestAppShell(agency));
      await tester.pumpAndSettle();

      expect(find.byType(JobProposalsScreen), findsOneWidget);
      expect(find.text('Proposals'), findsOneWidget);
      expect(find.text('Payments'), findsOneWidget);
      expect(find.byType(MyLoadsScreen), findsNothing);
      expect(find.byType(DriverAssignedTripScreen), findsNothing);
    });

    testWidgets('5. AppShell maps Driver role to DriverAssignedTripScreen and My Trip tab', (tester) async {
      const driver = AuthUser(
        userId: 'u-driver',
        email: 'driver@test.com',
        fullName: 'Driver User',
        role: 'Driver',
        isActive: true,
      );

      await tester.pumpWidget(createTestAppShell(driver));
      await tester.pumpAndSettle();

      expect(find.byType(DriverAssignedTripScreen), findsOneWidget);
      expect(find.text('My Trip'), findsOneWidget);
      // Drivers have no billing: no Payments tab, and only Dashboard + My Trip remain.
      expect(find.text('Payments'), findsNothing);
      expect(find.text('Dashboard'), findsOneWidget);
      expect(find.byType(MyLoadsScreen), findsNothing);
      expect(find.byType(JobProposalsScreen), findsNothing);
    });

    test('6. Admin login is blocked with explicit Web Portal direction policy', () async {

      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(
            jsonEncode({'accessToken': 'admin-jwt-token', 'refreshToken': 'admin-refresh-token'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        if (request.url.path.endsWith('/auth/me')) {
          return http.Response(
            jsonEncode({
              'userId': 'admin-1',
              'email': 'admin@freightlink.lk',
              'fullName': 'System Administrator',
              'role': 'Admin',
              'isActive': true,
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final authProvider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: client,
      );

      final success = await authProvider.login(
        email: 'admin@freightlink.lk',
        password: 'AdminPassword123!',
      );

      expect(success, isFalse);
      expect(authProvider.status, AuthStatus.guest);
      expect(
        authProvider.errorMessage,
        'Admin accounts can\'t sign in to the mobile app. Please use the web portal instead.',
      );
      verify(() => tokenStorage.clear()).called(1);
    });

    test('7. 401 Unauthorized invokes session clearing and switches status to guest', () async {

      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(
            jsonEncode({'accessToken': 'valid-token', 'refreshToken': 'valid-refresh-token'}),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        if (request.url.path.endsWith('/auth/me')) {
          return http.Response(
            jsonEncode({
              'userId': 'u-1',
              'email': 'user@test.com',
              'fullName': 'Test User',
              'role': 'Shipper',
              'isActive': true,
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        if (request.url.path.endsWith('/auth/refresh')) {
          return http.Response(
            jsonEncode({
              'error': {'code': 'INVALID_REFRESH_TOKEN', 'message': 'Refresh token expired.'}
            }),
            401,
            headers: {'content-type': 'application/json'},
          );
        }
        if (request.url.path.endsWith('/loads')) {
          return http.Response(
            jsonEncode({
              'error': {
                'code': 'UNAUTHORIZED',
                'message': 'Token expired.',
              }
            }),
            401,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final authProvider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: client,
      );

      await authProvider.login(email: 'user@test.com', password: 'Password123!');
      expect(authProvider.status, AuthStatus.authenticated);

      try {
        await authProvider.apiClient.get('/loads');
      } catch (_) {}

      expect(authProvider.status, AuthStatus.guest);
      expect(authProvider.user, isNull);
      verify(() => tokenStorage.clear()).called(1);
    });
  });
}
