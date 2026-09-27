import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/agencies/data/agencies_repository.dart';
import 'package:freightlink_mobile/features/agencies/screens/agency_dashboard_screen.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

class MockAgenciesRepository extends Mock implements AgenciesRepository {}
class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  late MockAgenciesRepository mockRepo;
  late MockAuthProvider mockAuth;

  final sampleStats = {
    'totalVehicles': 12,
    'activeDrivers': 8,
    'availableVehicles': 7,
    'onTripVehicles': 3,
    'maintenanceVehicles': 2,
    'pendingCompliance': 1,
  };

  final sampleProfile = {
    'name': 'Lanka Express Logistics',
    'status': 'Active',
    'businessRegNo': 'PV-12345',
    'yardAddress': 'Peliyagoda Logistics Park, Colombo',
  };

  const sampleUser = AuthUser(
    userId: 'user-1',
    email: 'dispatcher@lankaexpress.lk',
    fullName: 'Kamal Perera',
    role: 'AgencyStaff',
    isActive: true,
    agencyId: 'agency-1',
  );

  setUp(() {
    mockRepo = MockAgenciesRepository();
    mockAuth = MockAuthProvider();

    when(() => mockAuth.user).thenReturn(sampleUser);
    when(() => mockAuth.status).thenReturn(AuthStatus.authenticated);
  });

  Future<void> pumpDashboard(WidgetTester tester) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<AgenciesRepository>.value(value: mockRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
          ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: const AgencyDashboardScreen(),
        ),
      ),
    );
  }

  group('AgencyDashboardScreen', () {
    testWidgets('renders skeleton during initial loading state', (tester) async {
      final completer = Completer<Map<String, dynamic>>();
      when(() => mockRepo.getDashboardStats()).thenAnswer((_) => completer.future);
      when(() => mockRepo.getProfile()).thenAnswer((_) async => sampleProfile);

      await pumpDashboard(tester);
      await tester.pump();

      expect(find.text('Agency Dashboard'), findsOneWidget);
      expect(find.text('Fleet Operations & Dispatch'), findsOneWidget);

      completer.complete(sampleStats);
      await tester.pumpAndSettle();

      expect(find.text('Lanka Express Logistics'), findsOneWidget);
    });

    testWidgets('renders KPIs, fleet utilization, and action tiles when loaded', (tester) async {
      when(() => mockRepo.getDashboardStats()).thenAnswer((_) async => sampleStats);
      when(() => mockRepo.getProfile()).thenAnswer((_) async => sampleProfile);

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      // Hero banner
      expect(find.text('DISPATCH HUB'), findsOneWidget);
      expect(find.text('ACTIVE'), findsOneWidget);
      expect(find.text('Lanka Express Logistics'), findsOneWidget);
      expect(find.text('Peliyagoda Logistics Park, Colombo'), findsOneWidget);
      expect(find.text('Signed in as Kamal Perera'), findsOneWidget);

      // Section headers
      expect(find.text('OPERATIONAL OVERVIEW'), findsOneWidget);
      expect(find.text('DISPATCH & OPERATIONS'), findsOneWidget);
      expect(find.text('ADMINISTRATION & COMPLIANCE'), findsOneWidget);

      // KPI values
      expect(find.text('12'), findsOneWidget); // Total vehicles
      expect(find.text('Total Fleet'), findsOneWidget);
      expect(find.text('7 available'), findsOneWidget);

      expect(find.text('8'), findsOneWidget); // Active drivers
      expect(find.text('Active Drivers'), findsOneWidget);

      expect(find.text('3'), findsOneWidget); // On Trip
      expect(find.text('On Trip'), findsOneWidget);
      expect(find.text('3 active now'), findsOneWidget);

      expect(find.text('1'), findsOneWidget); // Pending KYC
      expect(find.text('Pending KYC'), findsOneWidget);
      expect(find.text('Action required'), findsOneWidget);

      // Fleet utilization card
      expect(find.text('FLEET AVAILABILITY'), findsOneWidget);
      expect(find.text('58% READY'), findsOneWidget); // 7/12 = 58%
      expect(find.text('Available: 7'), findsOneWidget);
      expect(find.text('On Trip: 3'), findsOneWidget);
      expect(find.text('Maint: 2'), findsOneWidget);

      // Action Tiles
      expect(find.text('Fleet Management'), findsOneWidget);
      expect(find.text('Job Proposals'), findsOneWidget);
      expect(find.text('Agency Trips'), findsOneWidget);
      expect(find.text('Billing & Invoices'), findsOneWidget);
      expect(find.text('Driver Onboarding'), findsOneWidget);
      expect(find.text('Compliance Documents'), findsOneWidget);
      expect(find.text('Agency Profile'), findsOneWidget);
    });

    testWidgets('renders ErrorState with retry button when error occurs', (tester) async {
      when(() => mockRepo.getDashboardStats()).thenThrow(Exception('Network timeout'));
      when(() => mockRepo.getProfile()).thenAnswer((_) async => {});

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('Unable to load dashboard'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);

      // Now mock success for retry
      when(() => mockRepo.getDashboardStats()).thenAnswer((_) async => sampleStats);
      when(() => mockRepo.getProfile()).thenAnswer((_) async => sampleProfile);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('Lanka Express Logistics'), findsOneWidget);
    });

    testWidgets('renders fallback greeting when agency profile is empty', (tester) async {
      when(() => mockRepo.getDashboardStats()).thenAnswer((_) async => sampleStats);
      when(() => mockRepo.getProfile()).thenAnswer((_) async => {});

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('Agency Fleet Hub'), findsOneWidget);
      expect(find.text('Primary Operations Yard'), findsOneWidget);
      expect(find.text('Signed in as Kamal Perera'), findsOneWidget);
    });
  });
}
