import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/driver_register_screen.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

void main() {
  setUpAll(registerFallbackValues);

  group('LoginScreen Driver Registration Link', () {
    late MockTokenStorage tokenStorage;

    setUp(() {
      tokenStorage = MockTokenStorage();
    });

    testWidgets('shows "Register here" when Driver role is selected and navigates to DriverRegisterScreen', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path.endsWith('/auth/agencies')) {
          return http.Response(
            jsonEncode([
              {'agencyId': 'ag-1', 'name': 'Speedy Express'},
            ]),
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

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider,
          child: const MaterialApp(
            home: LoginScreen(initialRole: LoginRole.shipper),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // For Shipper role, driver registration link should not be present
      expect(find.byKey(const Key('register_driver_link')), findsNothing);

      // Tap on Driver role chip
      await tester.tap(find.text('Driver'));
      await tester.pumpAndSettle();

      // Now "Register here" should appear
      expect(find.byKey(const Key('register_driver_link')), findsOneWidget);

      // Tap "Register here"
      await tester.tap(find.byKey(const Key('register_driver_link')));
      await tester.pumpAndSettle();

      // Navigated to DriverRegisterScreen
      expect(find.byType(DriverRegisterScreen), findsOneWidget);
    });
  });
}
