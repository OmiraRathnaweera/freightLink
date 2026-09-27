import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/login_screen.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

void main() {
  setUpAll(registerFallbackValues);

  group('LoginScreen Driver Role Notice', () {
    late MockTokenStorage tokenStorage;

    setUp(() {
      tokenStorage = MockTokenStorage();
    });

    testWidgets('shows a "contact your agency" notice instead of a self-registration link when Driver role is selected', (tester) async {
      final authProvider = AuthProvider(tokenStorage: tokenStorage);

      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider,
          child: const MaterialApp(
            home: LoginScreen(initialRole: LoginRole.shipper),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // For Shipper role, the driver notice should not be present
      expect(find.byKey(const Key('driver_registration_notice')), findsNothing);

      // Tap on Driver role chip
      await tester.tap(find.text('Driver'));
      await tester.pumpAndSettle();

      // Now the "contact your agency" notice should appear, with no registration link
      expect(find.byKey(const Key('driver_registration_notice')), findsOneWidget);
      expect(find.text('Register here'), findsNothing);
    });
  });
}
