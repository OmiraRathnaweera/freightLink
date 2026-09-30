// Smoke test for the login screen — the app's actual entry point now that
// `main.dart` no longer holds the counter template. Exercises `LoginScreen`
// directly (rather than the full `FreightLinkApp`) so it doesn't depend on
// `flutter_secure_storage`'s platform channel, which isn't available in the
// widget-test environment.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';

void main() {
  testWidgets('LoginScreen renders the sign-in form', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => AuthProvider(),
        child: MaterialApp(theme: AppTheme.light, home: const LoginScreen()),
      ),
    );

    expect(find.text('Email'), findsOneWidget);
    expect(find.text('Password'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'Sign in'), findsOneWidget);
  });
}
