import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

void main() {
  setUpAll(registerFallbackValues);

  testWidgets('renders one common login form with no registration link', (tester) async {
    final authProvider = AuthProvider(tokenStorage: MockTokenStorage());

    await tester.pumpWidget(
      ChangeNotifierProvider<AuthProvider>.value(
        value: authProvider,
        child: const MaterialApp(home: LoginScreen()),
      ),
    );

    expect(find.text('Email'), findsOneWidget);
    expect(find.text('Password'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'Sign in'), findsOneWidget);
    expect(find.text('Register agency'), findsNothing);
    expect(find.text('Shipper'), findsNothing);
    expect(find.text('Driver'), findsNothing);
  });
}
