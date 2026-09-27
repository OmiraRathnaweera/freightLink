import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/billing/data/billing_repository.dart';
import 'package:freightlink_mobile/features/billing/models/invoice.dart';
import 'package:freightlink_mobile/features/billing/screens/invoice_detail_screen.dart';
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

Invoice _invoice({required String status, String? paymentProofUrl}) {
  return Invoice.fromJson({
    'invoiceId': 'invoice-1',
    'invoiceNumber': 'INV-0001',
    'status': status,
    'totalAmount': 25000,
    'currency': 'LKR',
    'paymentProofUrl': paymentProofUrl,
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

  Future<void> pumpDetail(WidgetTester tester, {required AuthUser user}) async {
    when(() => mockAuth.user).thenReturn(user);
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<BillingRepository>.value(value: mockBillingRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: const InvoiceDetailScreen(invoiceId: 'invoice-1'),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  group('InvoiceDetailScreen — role x status action gating', () {
    testWidgets('Agency Staff sees Edit/Issue/Void for a Draft invoice, no Confirm Payment', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1')).thenAnswer((_) async => _invoice(status: 'Draft'));
      await pumpDetail(tester, user: _agencyStaff);

      expect(find.widgetWithText(OutlinedButton, 'Edit Draft'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Issue Invoice'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Void Invoice'), findsOneWidget);
      expect(find.text('Confirm Payment'), findsNothing);
      expect(find.textContaining('Submit'), findsNothing);
    });

    testWidgets('Agency Staff sees Confirm Payment only once a proof file is present', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1'))
          .thenAnswer((_) async => _invoice(status: 'PaymentPending'));
      await pumpDetail(tester, user: _agencyStaff);
      expect(find.text('Confirm Payment'), findsNothing);
    });

    testWidgets('Agency Staff sees Confirm Payment when PaymentPending with a proof file', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1')).thenAnswer(
        (_) async => _invoice(status: 'PaymentPending', paymentProofUrl: 'https://files/receipt.png'),
      );
      await pumpDetail(tester, user: _agencyStaff);
      expect(find.text('Confirm Payment'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Void Invoice'), findsOneWidget);
    });

    testWidgets('Agency Staff sees no actions at all for a terminal Paid invoice', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1')).thenAnswer((_) async => _invoice(status: 'Paid'));
      await pumpDetail(tester, user: _agencyStaff);

      expect(find.text('Edit Draft'), findsNothing);
      expect(find.text('Issue Invoice'), findsNothing);
      expect(find.text('Void Invoice'), findsNothing);
      expect(find.text('Confirm Payment'), findsNothing);
    });

    testWidgets('Shipper sees Submit Payment Receipt for an Issued invoice, no Agency actions', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1')).thenAnswer((_) async => _invoice(status: 'Issued'));
      await pumpDetail(tester, user: _shipper);

      expect(find.text('Submit Payment Receipt'), findsOneWidget);
      expect(find.text('Void Invoice'), findsNothing);
      expect(find.text('Issue Invoice'), findsNothing);
    });

    testWidgets('Shipper sees Replace Receipt once already PaymentPending', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1'))
          .thenAnswer((_) async => _invoice(status: 'PaymentPending'));
      await pumpDetail(tester, user: _shipper);

      expect(find.text('Replace Receipt'), findsOneWidget);
    });

    testWidgets('renders an ErrorState with retry when the fetch fails', (tester) async {
      when(() => mockBillingRepo.getInvoiceById('invoice-1')).thenThrow(
        const ApiException(statusCode: 500, code: 'INTERNAL_ERROR', message: 'Network timeout'),
      );
      await pumpDetail(tester, user: _agencyStaff);

      expect(find.text('Failed to Load Invoice'), findsOneWidget);
    });
  });
}
