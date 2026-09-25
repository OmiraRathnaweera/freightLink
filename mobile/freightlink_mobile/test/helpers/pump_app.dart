import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:provider/provider.dart';

/// Pumps [child] wrapped in the ancestor providers the shipper screens
/// expect (`LoadsRepository` via `context.read`, `AuthProvider` for
/// `AppAvatar`'s ancestor lookup) inside a themed `MaterialApp`.
///
/// [authProvider] defaults to a plain, un-bootstrapped `AuthProvider()` —
/// matching the existing `test/widget_test.dart` smoke test's approach of
/// never calling `bootstrap()`/touching `flutter_secure_storage`'s platform
/// channel in a widget test.
Future<void> pumpApp(
  WidgetTester tester,
  Widget child, {
  required LoadsRepository repository,
  AuthProvider? authProvider,
}) async {
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        Provider<LoadsRepository>.value(value: repository),
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider ?? AuthProvider(),
        ),
      ],
      child: MaterialApp(theme: AppTheme.light, home: child),
    ),
  );
}
