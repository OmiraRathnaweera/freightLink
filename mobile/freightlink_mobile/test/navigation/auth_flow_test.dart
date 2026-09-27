import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/core/routing/app_router.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/loads/screens/my_loads_screen.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/screens/driver_assigned_trip_screen.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../helpers/fakes.dart';
import '../helpers/fixtures.dart';

/// A fake `/auth/login` + `/auth/me` backend, so `AuthProvider.login()` and
/// `bootstrap()` run for real (no shortcuts on the provider itself) without
/// ever touching a real network — the `httpClient` seam added to
/// `AuthProvider` for exactly this purpose.
http.Client fakeAuthClient({
  String role = 'Shipper',
  String email = 'shipper@test.com',
  String fullName = 'Sam Shipper',
}) {
  return MockClient((request) async {
    if (request.url.path.endsWith('/auth/login')) {
      return http.Response(
        jsonEncode({'accessToken': 'test-token', 'refreshToken': 'test-refresh-token'}),
        200,
        headers: {'content-type': 'application/json'},
      );
    }
    if (request.url.path.endsWith('/auth/refresh')) {
      return http.Response(
        jsonEncode({'accessToken': 'refreshed-token', 'refreshToken': 'rotated-refresh-token'}),
        200,
        headers: {'content-type': 'application/json'},
      );
    }
    if (request.url.path.endsWith('/auth/me')) {
      return http.Response(
        jsonEncode({
          'userId': 'user-1',
          'email': email,
          'fullName': fullName,
          'role': role,
          'isActive': true,
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    }
    return http.Response('Not found', 404);
  });
}

void main() {
  setUpAll(registerFallbackValues);

  late MockLoadsRepository repository;
  late MockTokenStorage tokenStorage;

  setUp(() {
    repository = MockLoadsRepository();
    tokenStorage = MockTokenStorage();
    when(() => tokenStorage.readRefreshToken()).thenAnswer((_) async => null);
    when(() => tokenStorage.saveTokenPair(accessToken: any(named: 'accessToken'), refreshToken: any(named: 'refreshToken'))).thenAnswer((_) async {});
    when(() => tokenStorage.clear()).thenAnswer((_) async {});
    when(
      () => repository.getList(
        search: any(named: 'search'),
        status: any(named: 'status'),
      ),
    ).thenAnswer((_) async => buildPagedResult(const []));
  });

  Future<void> pumpRoot(
    WidgetTester tester,
    AuthProvider authProvider, {
    TripsRepository? tripsRepository,
  }) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
          ChangeNotifierProvider<NotificationProvider>(
            create: (_) => NotificationProvider(),
          ),
          Provider<LoadsRepository>.value(value: repository),
          Provider<TripsRepository>.value(
            value: tripsRepository ?? MockTripsRepository(),
          ),
        ],
        child: MaterialApp.router(
          theme: AppTheme.light,
          routerConfig: createAppRouter(authProvider),
        ),
      ),
    );
  }

  group('Auth navigation', () {
    testWidgets('login success lands on My Loads', (tester) async {
      when(() => tokenStorage.readAccessToken()).thenAnswer((_) async => null);

      final authProvider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: fakeAuthClient(),
      );
      await authProvider.bootstrap();
      expect(authProvider.status, AuthStatus.guest);

      await pumpRoot(tester, authProvider);
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsOneWidget);

      await tester.enterText(find.byType(TextField).first, 'shipper@test.com');
      await tester.enterText(find.byType(TextField).last, 'password123');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(authProvider.status, AuthStatus.authenticated);
      expect(find.byType(MyLoadsScreen), findsOneWidget);
      expect(find.byType(LoginScreen), findsNothing);
    });

    testWidgets('driver login success lands on DriverAssignedTripScreen', (tester) async {
      when(() => tokenStorage.readAccessToken()).thenAnswer((_) async => null);

      final mockTripsRepo = MockTripsRepository();
      when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => null);

      final authProvider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: fakeAuthClient(
          role: 'Driver',
          email: 'driver1@freightlink.lk',
          fullName: 'Sunil Perera',
        ),
      );
      await authProvider.bootstrap();
      expect(authProvider.status, AuthStatus.guest);

      await pumpRoot(tester, authProvider, tripsRepository: mockTripsRepo);
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsOneWidget);

      await tester.enterText(find.byType(TextField).first, 'driver1@freightlink.lk');
      await tester.enterText(find.byType(TextField).last, 'Password123!');

      await tester.tap(find.widgetWithText(ElevatedButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(authProvider.status, AuthStatus.authenticated);
      expect(find.byType(DriverAssignedTripScreen), findsOneWidget);
      expect(find.text('My Trip'), findsOneWidget);
      expect(find.byType(LoginScreen), findsNothing);
    });

    testWidgets(
      'logging out from an authenticated session redirects to login',
      (tester) async {
        when(() => tokenStorage.readRefreshToken()).thenAnswer((_) async => 'stored-refresh-token');

        final authProvider = AuthProvider(
          tokenStorage: tokenStorage,
          httpClient: fakeAuthClient(),
        );
        await authProvider.bootstrap();
        expect(authProvider.status, AuthStatus.authenticated);

        await pumpRoot(tester, authProvider);
        await tester.pumpAndSettle();

        expect(find.byType(MyLoadsScreen), findsOneWidget);

        await authProvider.logout();
        await tester.pumpAndSettle();

        expect(find.byType(LoginScreen), findsOneWidget);
        expect(find.byType(MyLoadsScreen), findsNothing);
        verify(() => tokenStorage.clear()).called(1);
      },
    );
  });
}
