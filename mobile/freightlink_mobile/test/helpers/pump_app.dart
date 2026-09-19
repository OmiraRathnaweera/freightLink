import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:provider/provider.dart';

import 'fakes.dart';

/// Pumps [child] wrapped in ancestor providers inside a themed [MaterialApp].
Future<void> pumpApp(
  WidgetTester tester,
  Widget child, {
  LoadsRepository? repository,
  TripsRepository? tripsRepository,
  AuthProvider? authProvider,
}) async {
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        Provider<LoadsRepository>.value(
          value: repository ?? MockLoadsRepository(),
        ),
        Provider<TripsRepository>.value(
          value: tripsRepository ?? MockTripsRepository(),
        ),
        ChangeNotifierProvider<AuthProvider>.value(
          value: authProvider ?? AuthProvider(),
        ),
      ],
      child: MaterialApp(theme: AppTheme.light, home: child),
    ),
  );
}
