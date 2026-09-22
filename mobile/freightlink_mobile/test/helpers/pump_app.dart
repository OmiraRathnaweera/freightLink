import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
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
  NotificationProvider? notificationProvider,
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
        authProvider != null
            ? ChangeNotifierProvider<AuthProvider>.value(value: authProvider)
            : ChangeNotifierProvider<AuthProvider>(create: (_) => AuthProvider()),
        notificationProvider != null
            ? ChangeNotifierProvider<NotificationProvider>.value(value: notificationProvider)
            : ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
      ],
      child: MaterialApp(theme: AppTheme.light, home: child),
    ),
  );
}
