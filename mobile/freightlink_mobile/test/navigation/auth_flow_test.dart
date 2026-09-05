import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/loads/screens/my_loads_screen.dart';
import 'package:freightlink_mobile/main.dart';
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
http.Client fakeAuthClient() {
  return MockClient((request) async {
    if (request.url.path.endsWith('/auth/login')) {
      return http.Response(
        jsonEncode({'accessToken': 'test-token'}),
        200,
        headers: {'content-type': 'application/json'},
      );
    }
    if (request.url.path.endsWith('/auth/me')) {
      return http.Response(
        jsonEncode({
          'userId': 'user-1',
          'email': 'shipper@test.com',
          'fullName': 'Sam Shipper',
          'role': 'Shipper',
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
    when(
      () => repository.getList(
        search: any(named: 'search'),
        status: any(named: 'status'),
      ),
    ).thenAnswer((_) async => buildPagedResult(const []));
  });

  Future<void> pumpRoot(WidgetTester tester, AuthProvider authProvider) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
          Provider<LoadsRepository>.value(value: repository),
        ],
        child: MaterialApp(theme: AppTheme.light, home: const RootScreen()),
      ),
    );
  }

  group('Auth navigation', () {
    testWidgets('login success lands on My Loads', (tester) async {
      when(() => tokenStorage.readAccessToken()).thenAnswer((_) async => null);
      when(() => tokenStorage.saveAccessToken(any())).thenAnswer((_) async {});

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

    testWidgets(
      'logging out from an authenticated session redirects to login',
      (tester) async {
        when(
          () => tokenStorage.readAccessToken(),
        ).thenAnswer((_) async => 'stored-token');
        when(() => tokenStorage.clear()).thenAnswer((_) async {});

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
