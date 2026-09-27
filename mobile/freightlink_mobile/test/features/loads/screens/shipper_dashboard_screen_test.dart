import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/billing/data/billing_repository.dart';
import 'package:freightlink_mobile/features/billing/models/invoice.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/loads/models/load.dart';
import 'package:freightlink_mobile/features/loads/models/load_status.dart';
import 'package:freightlink_mobile/features/loads/screens/shipper_dashboard_screen.dart';
import 'package:freightlink_mobile/features/notifications/providers/notification_provider.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  late MockLoadsRepository mockLoadsRepo;
  late MockBillingRepository mockBillingRepo;
  late MockAuthProvider mockAuth;

  const sampleUser = AuthUser(
    userId: 'user-1',
    email: 'shipper@freightlink.lk',
    fullName: 'Nadeesha Fernando',
    role: 'Shipper',
    isActive: true,
  );

  PagedResult<LoadListItem> paged(int totalItems) => PagedResult<LoadListItem>(
        items: const [],
        page: 1,
        pageSize: 1,
        totalItems: totalItems,
        totalPages: totalItems == 0 ? 0 : 1,
      );

  setUp(() {
    mockLoadsRepo = MockLoadsRepository();
    mockBillingRepo = MockBillingRepository();
    mockAuth = MockAuthProvider();

    when(() => mockAuth.user).thenReturn(sampleUser);
    when(() => mockAuth.status).thenReturn(AuthStatus.authenticated);
  });

  void stubCounts({
    int total = 0,
    int posted = 0,
    int matched = 0,
    int inTransit = 0,
    int delivered = 0,
  }) {
    when(() => mockLoadsRepo.getList(pageSize: 1)).thenAnswer((_) async => paged(total));
    when(() => mockLoadsRepo.getList(pageSize: 1, status: LoadStatus.posted))
        .thenAnswer((_) async => paged(posted));
    when(() => mockLoadsRepo.getList(pageSize: 1, status: LoadStatus.matched))
        .thenAnswer((_) async => paged(matched));
    when(() => mockLoadsRepo.getList(pageSize: 1, status: LoadStatus.inTransit))
        .thenAnswer((_) async => paged(inTransit));
    when(() => mockLoadsRepo.getList(pageSize: 1, status: LoadStatus.delivered))
        .thenAnswer((_) async => paged(delivered));
  }

  Future<void> pumpDashboard(WidgetTester tester) async {
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<LoadsRepository>.value(value: mockLoadsRepo),
          Provider<BillingRepository>.value(value: mockBillingRepo),
          ChangeNotifierProvider<AuthProvider>.value(value: mockAuth),
          ChangeNotifierProvider<NotificationProvider>(create: (_) => NotificationProvider()),
        ],
        child: MaterialApp(
          theme: AppTheme.light,
          home: const ShipperDashboardScreen(),
        ),
      ),
    );
  }

  group('ShipperDashboardScreen', () {
    testWidgets('renders KPIs and quick actions when loaded', (tester) async {
      stubCounts(total: 9, posted: 2, matched: 1, inTransit: 1, delivered: 5);
      when(() => mockBillingRepo.getInvoices()).thenAnswer(
        (_) async => [
          _sampleInvoice(status: 'Issued'),
          _sampleInvoice(status: 'Paid'),
        ],
      );

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      // Hero banner
      expect(find.text('SHIPPER PORTAL'), findsOneWidget);
      expect(find.text('Nadeesha Fernando'), findsOneWidget);
      expect(find.text('shipper@freightlink.lk'), findsOneWidget);

      // Section headers
      expect(find.text('SHIPMENTS OVERVIEW'), findsOneWidget);
      expect(find.text('QUICK ACTIONS'), findsOneWidget);

      // KPI values
      expect(find.text('2'), findsOneWidget); // Awaiting Match
      expect(find.text('Awaiting Match'), findsOneWidget);
      expect(find.text('1'), findsNWidgets(2)); // Matched + In Transit
      expect(find.text('Matched'), findsOneWidget);
      expect(find.text('In Transit'), findsOneWidget);
      expect(find.text('5'), findsOneWidget); // Delivered
      expect(find.text('Delivered'), findsOneWidget);

      // Quick actions (StatusPill badges render their label uppercased)
      expect(find.text('Post a Load'), findsOneWidget);
      expect(find.text('My Loads'), findsOneWidget);
      expect(find.text('9 TOTAL'), findsOneWidget);
      expect(find.text('Payments & Invoices'), findsOneWidget);
      expect(find.text('1 DUE'), findsOneWidget);
    });

    testWidgets('shows "Up to date" when there are no pending invoices', (tester) async {
      stubCounts();
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) async => []);

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('UP TO DATE'), findsOneWidget);
    });

    testWidgets('renders ErrorState with retry button when the loads fetch fails', (tester) async {
      when(() => mockLoadsRepo.getList(pageSize: 1)).thenThrow(Exception('Network timeout'));

      await pumpDashboard(tester);
      await tester.pumpAndSettle();

      expect(find.text('Unable to load dashboard'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);

      stubCounts(total: 3, posted: 3);
      when(() => mockBillingRepo.getInvoices()).thenAnswer((_) async => []);

      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(find.text('SHIPMENTS OVERVIEW'), findsOneWidget);
    });
  });
}

Invoice _sampleInvoice({required String status}) {
  return Invoice.fromJson({
    'invoiceId': 'invoice-${status.toLowerCase()}',
    'invoiceNumber': 'INV-0001',
    'status': status,
    'totalAmount': 1000,
    'currency': 'LKR',
    'createdAt': '2026-09-01T00:00:00Z',
    'updatedAt': '2026-09-01T00:00:00Z',
  });
}
