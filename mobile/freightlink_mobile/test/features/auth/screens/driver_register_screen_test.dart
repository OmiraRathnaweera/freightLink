import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/driver_register_screen.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

void main() {
  setUpAll(registerFallbackValues);

  group('DriverRegisterScreen', () {
    late MockTokenStorage tokenStorage;

    setUp(() {
      tokenStorage = MockTokenStorage();
    });

    http.Client buildMockClient({
      List<Map<String, dynamic>>? agencies,
      int registerStatus = 201,
      Map<String, dynamic>? registerBody,
    }) {
      return MockClient((request) async {
        if (request.url.path.endsWith('/auth/agencies')) {
          return http.Response(
            jsonEncode(
              agencies ??
                  [
                    {'agencyId': 'agency-uuid-1', 'name': 'Speedy Express'},
                    {'agencyId': 'agency-uuid-2', 'name': 'Lanka Cargo'},
                  ],
            ),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        if (request.url.path.endsWith('/auth/register/driver')) {
          if (registerStatus == 201) {
            return http.Response(
              jsonEncode({
                'message': 'Driver registered successfully.',
                'userId': 'usr-1',
                'email': 'driver@test.com',
              }),
              201,
              headers: {'content-type': 'application/json'},
            );
          } else {
            return http.Response(
              jsonEncode(
                registerBody ??
                    {
                      'error': {
                        'code': 'DRIVER_LICENCE_ALREADY_REGISTERED',
                        'message': 'Licence already registered.',
                        'details': [],
                      }
                    },
              ),
              registerStatus,
              headers: {'content-type': 'application/json'},
            );
          }
        }
        return http.Response('Not found', 404);
      });
    }

    Future<void> pumpRegisterScreen(
      WidgetTester tester, {
      http.Client? httpClient,
    }) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      final authProvider = AuthProvider(
        tokenStorage: tokenStorage,
        httpClient: httpClient ?? buildMockClient(),
      );

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider,
          child: MaterialApp(
            theme: AppTheme.light,
            home: Builder(
              builder: (context) => Scaffold(
                body: ElevatedButton(
                  key: const Key('open_register_btn'),
                  onPressed: () {
                    Navigator.of(context).push(
                      MaterialPageRoute(
                        builder: (_) => const DriverRegisterScreen(),
                      ),
                    );
                  },
                  child: const Text('Open'),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.byKey(const Key('open_register_btn')));
      await tester.pumpAndSettle();
    }

    testWidgets('renders all fields and loads agencies dropdown', (tester) async {
      await pumpRegisterScreen(tester);

      expect(find.text('Driver Registration'), findsOneWidget);
      expect(find.text('Join FreightLink as Driver'), findsOneWidget);
      expect(find.byKey(const Key('register_agency_dropdown')), findsOneWidget);
      expect(find.byKey(const Key('register_name_field')), findsOneWidget);
      expect(find.byKey(const Key('register_email_field')), findsOneWidget);
      expect(find.byKey(const Key('register_phone_field')), findsOneWidget);
      expect(find.byKey(const Key('register_licence_field')), findsOneWidget);
      expect(find.byKey(const Key('register_expiry_field')), findsOneWidget);
      expect(find.byKey(const Key('register_password_field')), findsOneWidget);
      expect(find.byKey(const Key('register_confirm_password_field')), findsOneWidget);
      expect(find.byKey(const Key('register_submit_button')), findsOneWidget);
    });

    testWidgets('shows validation error when required fields are empty', (tester) async {
      await pumpRegisterScreen(tester);

      // Scroll to submit button and tap with empty fields
      await tester.ensureVisible(find.byKey(const Key('register_submit_button')));
      await tester.tap(find.byKey(const Key('register_submit_button')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.byKey(const Key('register_error_banner')));
      expect(find.byKey(const Key('register_error_banner')), findsOneWidget);
      expect(find.text('Full name must be at least 2 characters.'), findsOneWidget);
    });

    testWidgets('successful registration pops and shows snackbar', (tester) async {
      await pumpRegisterScreen(tester);

      // Select agency
      await tester.ensureVisible(find.byKey(const Key('register_agency_dropdown')));
      await tester.tap(find.byKey(const Key('register_agency_dropdown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Speedy Express').last);
      await tester.pumpAndSettle();

      // Enter Name
      await tester.ensureVisible(find.byKey(const Key('register_name_field')));
      await tester.enterText(find.byKey(const Key('register_name_field')), 'Kamal Silva');

      // Enter Email
      await tester.ensureVisible(find.byKey(const Key('register_email_field')));
      await tester.enterText(find.byKey(const Key('register_email_field')), 'kamal@driver.lk');

      // Enter Phone
      await tester.ensureVisible(find.byKey(const Key('register_phone_field')));
      await tester.enterText(find.byKey(const Key('register_phone_field')), '+94771234567');

      // Enter Licence
      await tester.ensureVisible(find.byKey(const Key('register_licence_field')));
      await tester.enterText(find.byKey(const Key('register_licence_field')), 'B1234567');

      // Tap Expiry Date field to open date picker
      await tester.ensureVisible(find.byKey(const Key('register_expiry_field')));
      await tester.tap(find.byKey(const Key('register_expiry_field')));
      await tester.pumpAndSettle();
      // Select date in picker and tap OK
      await tester.tap(find.text('OK'));
      await tester.pumpAndSettle();

      // Enter Password
      await tester.ensureVisible(find.byKey(const Key('register_password_field')));
      await tester.enterText(find.byKey(const Key('register_password_field')), 'StrongPass1!');

      // Enter Confirm Password
      await tester.ensureVisible(find.byKey(const Key('register_confirm_password_field')));
      await tester.enterText(find.byKey(const Key('register_confirm_password_field')), 'StrongPass1!');

      // Submit
      await tester.ensureVisible(find.byKey(const Key('register_submit_button')));
      await tester.tap(find.byKey(const Key('register_submit_button')));
      await tester.pump();
      await tester.pumpAndSettle();

      // Success snackbar is shown
      expect(find.text('Registration successful! Please sign in with your credentials.'), findsOneWidget);
    });
  });
}
