import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/auth/screens/account_settings_screen.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  late MockAuthProvider mockAuth;

  const sampleUser = AuthUser(
    userId: 'user-1',
    email: 'shipper@freightlink.lk',
    fullName: 'Nadeesha Fernando',
    role: 'Shipper',
    isActive: true,
    phoneE164: '+94711234567',
  );

  setUp(() {
    mockAuth = MockAuthProvider();
    when(() => mockAuth.user).thenReturn(sampleUser);
    when(() => mockAuth.isSubmitting).thenReturn(false);
    when(() => mockAuth.errorMessage).thenReturn(null);
  });

  Future<void> pumpScreen(WidgetTester tester) async {
    await tester.pumpWidget(
      ChangeNotifierProvider<AuthProvider>.value(
        value: mockAuth,
        child: const MaterialApp(home: AccountSettingsScreen()),
      ),
    );
  }

  group('AccountSettingsScreen — Profile section', () {
    testWidgets('pre-fills fields from the signed-in user', (tester) async {
      await pumpScreen(tester);

      expect(find.widgetWithText(TextField, 'Nadeesha Fernando'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'shipper@freightlink.lk'), findsOneWidget);
      expect(find.widgetWithText(TextField, '+94711234567'), findsOneWidget);
    });

    testWidgets('Save Changes calls updateProfile with the edited values', (tester) async {
      when(() => mockAuth.updateProfile(
            fullName: any(named: 'fullName'),
            email: any(named: 'email'),
            phoneE164: any(named: 'phoneE164'),
          )).thenAnswer((_) async => true);

      await pumpScreen(tester);

      await tester.enterText(find.widgetWithText(TextField, 'Nadeesha Fernando'), 'Nadeesha Updated');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Save Changes'));
      await tester.pumpAndSettle();

      verify(() => mockAuth.updateProfile(
            fullName: 'Nadeesha Updated',
            email: 'shipper@freightlink.lk',
            phoneE164: '+94711234567',
          )).called(1);
      expect(find.text('Profile updated successfully.'), findsOneWidget);
    });

    testWidgets('shows the backend error message when updateProfile fails', (tester) async {
      when(() => mockAuth.updateProfile(
            fullName: any(named: 'fullName'),
            email: any(named: 'email'),
            phoneE164: any(named: 'phoneE164'),
          )).thenAnswer((_) async => false);
      when(() => mockAuth.errorMessage).thenReturn('An account with this email already exists.');

      await pumpScreen(tester);

      await tester.tap(find.widgetWithText(ElevatedButton, 'Save Changes'));
      await tester.pumpAndSettle();

      expect(find.text('An account with this email already exists.'), findsOneWidget);
    });
  });

  group('AccountSettingsScreen — Password section', () {
    testWidgets('rejects a weak new password client-side without calling changePassword', (tester) async {
      await pumpScreen(tester);

      await tester.enterText(find.byType(TextField).at(3), 'CurrentPass1!');
      await tester.enterText(find.byType(TextField).at(4), 'weak');
      final changePasswordButton = find.widgetWithText(ElevatedButton, 'Change Password');
      await tester.ensureVisible(changePasswordButton);
      await tester.tap(changePasswordButton);
      await tester.pumpAndSettle();

      verifyNever(() => mockAuth.changePassword(
            currentPassword: any(named: 'currentPassword'),
            newPassword: any(named: 'newPassword'),
          ));
      expect(
        find.text('Must be at least 8 characters with an uppercase letter, a lowercase letter, a digit, and a special character.'),
        findsOneWidget,
      );
    });

    testWidgets('Change Password calls changePassword with valid input', (tester) async {
      when(() => mockAuth.changePassword(
            currentPassword: any(named: 'currentPassword'),
            newPassword: any(named: 'newPassword'),
          )).thenAnswer((_) async => true);

      await pumpScreen(tester);

      await tester.enterText(find.byType(TextField).at(3), 'CurrentPass1!');
      await tester.enterText(find.byType(TextField).at(4), 'N3w\$trongPass!');
      final changePasswordButton = find.widgetWithText(ElevatedButton, 'Change Password');
      await tester.ensureVisible(changePasswordButton);
      await tester.tap(changePasswordButton);
      await tester.pumpAndSettle();

      verify(() => mockAuth.changePassword(
            currentPassword: 'CurrentPass1!',
            newPassword: 'N3w\$trongPass!',
          )).called(1);
      expect(find.text('Password changed. Please sign in again.'), findsOneWidget);
    });

    testWidgets('shows the backend error message when changePassword fails', (tester) async {
      when(() => mockAuth.changePassword(
            currentPassword: any(named: 'currentPassword'),
            newPassword: any(named: 'newPassword'),
          )).thenAnswer((_) async => false);
      when(() => mockAuth.errorMessage).thenReturn('The current password you entered is incorrect.');

      await pumpScreen(tester);

      await tester.enterText(find.byType(TextField).at(3), 'WrongPass1!');
      await tester.enterText(find.byType(TextField).at(4), 'N3w\$trongPass!');
      final changePasswordButton = find.widgetWithText(ElevatedButton, 'Change Password');
      await tester.ensureVisible(changePasswordButton);
      await tester.tap(changePasswordButton);
      await tester.pumpAndSettle();

      expect(find.text('The current password you entered is incorrect.'), findsOneWidget);
    });
  });
}
