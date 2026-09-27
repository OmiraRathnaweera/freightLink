import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/billing/data/billing_repository.dart';
import 'package:freightlink_mobile/features/billing/models/invoice.dart';
import 'package:freightlink_mobile/features/billing/screens/payments_screen.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

const _shipper = AuthUser(
  userId: 'shipper-1',
  email: 'shipper@freightlink.lk',
  fullName: 'Nadeesha Fernando',
  role: 'Shipper',
  isActive: true,
);

const _agencyStaff = AuthUser(
  userId: 'staff-1',
  email: 'staff@freightlink.lk',
  fullName: 'Kasun Perera',
  role: 'AgencyStaff',
  isActive: true,
);

Invoice _invoice({String status = 'Issued', String? tripId}) {
  return Invoice.fromJson({
    'invoiceId': 'invoice-1',
    'invoiceNumber': 'INV-0001',
    'status': status,
    'totalAmount': 25000,
    'currency': 'LKR',
    'tripId': tripId,
    'createdAt': '2026-09-01T00:00:00Z',
    'updatedAt': '2026-09-01T00:00:00Z',
  });
}

void main() {
  late MockBillingRepository mockBillingRepo;
  late MockAuthProvider mockAuth;

  setUp(() {
    mockBillingRepo = MockBillingRepository();
    mockAuth = MockAuthProvider();
    when(() => mockAuth.status).thenReturn(AuthStatus.authenticated);
  });

  Future<void> pumpPayments(WidgetTester tester, {AuthUser user = _shipper}) async {
    when(() => mockAuth.user).thenReturn(user);
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<BillingRepository>.value(value: mockBillingRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
          ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
        ],
        child: MaterialApp(theme: AppTheme.light, home: const PaymentsScreen()),
      ),
    );
  }

  group('PaymentsScreen', () {
    testWidgets('shows a loading indicator while the query is in flight', (tester) async {
      // A never-completing Future (rather than Future.delayed) keeps the screen in its loading
      // state without leaving a pending Timer behind when the test ends.
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) => Completer<List<Invoice>>().future);
      await pumpPayments(tester);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('renders an empty message with no fabricated invoices', (tester) async {
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) async => <Invoice>[]);
      await pumpPayments(tester);
      await tester.pumpAndSettle();

      expect(find.text('No invoices are available yet.'), findsOneWidget);
      expect(find.textContaining('INV-'), findsNothing);
    });

    testWidgets('renders a retry action on failure', (tester) async {
      when(() => mockBillingRepo.getInvoices()).thenThrow(
        const ApiException(statusCode: 500, code: 'INTERNAL_ERROR', message: 'Network timeout'),
      );
      await pumpPayments(tester);
      await tester.pumpAndSettle();

      expect(find.text('Unable to load invoices'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('offers "View details" (not "Manage") to a Shipper', (tester) async {
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) async => [_invoice()]);
      await pumpPayments(tester, user: _shipper);
      await tester.pumpAndSettle();

      expect(find.text('View details'), findsOneWidget);
      expect(find.text('Manage'), findsNothing);
    });

    testWidgets('offers "Manage" (not "View details") to Agency Staff', (tester) async {
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) async => [_invoice()]);
      await pumpPayments(tester, user: _agencyStaff);
      await tester.pumpAndSettle();

      expect(find.text('Manage'), findsOneWidget);
      expect(find.text('View details'), findsNothing);
    });
  });
}
