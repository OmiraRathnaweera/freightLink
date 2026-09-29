import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/billing/data/billing_repository.dart';
import 'package:freightlink_mobile/features/billing/models/invoice.dart';
import 'package:freightlink_mobile/features/billing/screens/create_invoice_screen.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../../helpers/fakes.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

const _agencyStaff = AuthUser(
  userId: 'staff-1',
  email: 'staff@freightlink.lk',
  fullName: 'Kasun Perera',
  role: 'AgencyStaff',
  isActive: true,
);

const _shipper = AuthUser(
  userId: 'shipper-1',
  email: 'shipper@freightlink.lk',
  fullName: 'Nadeesha Fernando',
  role: 'Shipper',
  isActive: true,
);

TripResponse _sampleTrip({double? agreedPrice = 45000}) {
  return TripResponse(
    tripId: 'trip-1',
    assignmentId: 'assignment-1',
    loadId: 'load-1',
    agencyId: 'agency-1',
    vehicleId: 'vehicle-1',
    driverId: 'driver-1',
    status: 'Delivered',
    referenceCode: 'LD-CMB-KDY-01',
    shipperUserId: 'shipper-1',
    shipperName: 'Acme Traders',
    agreedPrice: agreedPrice,
    createdAt: DateTime.utc(2026, 9, 1),
  );
}

void main() {
  late MockBillingRepository mockBillingRepo;
  late MockTripsRepository mockTripsRepo;
  late MockAuthProvider mockAuth;

  setUp(() {
    mockBillingRepo = MockBillingRepository();
    mockTripsRepo = MockTripsRepository();
    mockAuth = MockAuthProvider();
    when(() => mockAuth.status).thenReturn(AuthStatus.authenticated);
  });

  Future<void> pumpForm(
    WidgetTester tester, {
    required AuthUser user,
    String? tripId,
    Invoice? editingInvoice,
  }) async {
    when(() => mockAuth.user).thenReturn(user);
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<BillingRepository>.value(value: mockBillingRepo),
          Provider<TripsRepository>.value(value: mockTripsRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: CreateInvoiceScreen(tripId: tripId, editingInvoice: editingInvoice),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  group('CreateInvoiceScreen — create mode (trip-linked, single amount)', () {
    testWidgets('blocks non-Agency-Staff roles from the form', (tester) async {
      await pumpForm(tester, user: _shipper, tripId: 'trip-1');
      expect(find.text('Only Agency Staff can manage invoices.'), findsOneWidget);
      expect(find.text('Create & Issue'), findsNothing);
    });

    testWidgets('shows an ErrorState with retry when the trip fails to load', (tester) async {
      when(() => mockTripsRepo.getTripById('trip-1')).thenThrow(
        const ApiException(statusCode: 500, code: 'INTERNAL_ERROR', message: 'Network timeout'),
      );
      await pumpForm(tester, user: _agencyStaff, tripId: 'trip-1');

      expect(find.text('Failed to Load Trip'), findsOneWidget);
      expect(find.text('Network timeout'), findsOneWidget);
    });

    testWidgets('pre-fills the amount from the trip’s agreed price and shows the shipper', (tester) async {
      when(() => mockTripsRepo.getTripById('trip-1')).thenAnswer((_) async => _sampleTrip());
      await pumpForm(tester, user: _agencyStaff, tripId: 'trip-1');

      expect(find.text('LD-CMB-KDY-01'), findsOneWidget);
      expect(find.text('Acme Traders'), findsOneWidget);
      expect(find.widgetWithText(TextFormField, 'Amount (LKR)'), findsOneWidget);
      expect(find.text('45000'), findsOneWidget);
    });

    testWidgets('rejects submission with a zero amount', (tester) async {
      when(() => mockTripsRepo.getTripById('trip-1')).thenAnswer((_) async => _sampleTrip(agreedPrice: null));
      await pumpForm(tester, user: _agencyStaff, tripId: 'trip-1');

      await tester.tap(find.text('Save as Draft'));
      await tester.pumpAndSettle();

      expect(find.text('Enter an amount greater than zero.'), findsOneWidget);
      verifyNever(() => mockBillingRepo.createInvoice(
            tripId: any(named: 'tripId'),
            amount: any(named: 'amount'),
            notes: any(named: 'notes'),
            issueImmediately: any(named: 'issueImmediately'),
          ));
    });

    testWidgets('submits {tripId, amount, notes, issueImmediately} with the pre-filled amount', (tester) async {
      when(() => mockTripsRepo.getTripById('trip-1')).thenAnswer((_) async => _sampleTrip());
      when(() => mockBillingRepo.createInvoice(
            tripId: any(named: 'tripId'),
            amount: any(named: 'amount'),
            notes: any(named: 'notes'),
            issueImmediately: any(named: 'issueImmediately'),
          )).thenAnswer(
        (_) async => Invoice.fromJson({
          'invoiceId': 'invoice-1',
          'invoiceNumber': 'INV-0001',
          'status': 'Issued',
          'totalAmount': 45000,
          'currency': 'LKR',
          'createdAt': '2026-09-01T00:00:00Z',
          'updatedAt': '2026-09-01T00:00:00Z',
        }),
      );

      await pumpForm(tester, user: _agencyStaff, tripId: 'trip-1');
      await tester.tap(find.text('Create & Issue'));
      await tester.pumpAndSettle();

      verify(() => mockBillingRepo.createInvoice(
            tripId: 'trip-1',
            amount: 45000,
            notes: any(named: 'notes'),
            issueImmediately: true,
          )).called(1);
    });
  });

  group('CreateInvoiceScreen — edit mode', () {
    testWidgets('pre-fills the amount from the existing invoice and never fetches a trip', (tester) async {
      await pumpForm(
        tester,
        user: _agencyStaff,
        editingInvoice: Invoice.fromJson({
          'invoiceId': 'inv-1',
          'invoiceNumber': 'INV-0001',
          'recipientName': 'Acme Traders',
          'status': 'Draft',
          'totalAmount': 45000,
          'currency': 'LKR',
          'createdAt': '2026-09-01T00:00:00Z',
          'updatedAt': '2026-09-01T00:00:00Z',
        }),
      );

      expect(find.text('INV-0001'), findsOneWidget);
      expect(find.text('Acme Traders'), findsOneWidget);
      expect(find.text('45000'), findsOneWidget);
      expect(find.text('Save Changes'), findsOneWidget);
      verifyNever(() => mockTripsRepo.getTripById(any()));
    });
  });
}
