import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/theme/app_theme.dart';
import 'package:freightlink_mobile/features/disputes/data/dispute_repository.dart';
import 'package:freightlink_mobile/features/disputes/models/dispute.dart';
import 'package:freightlink_mobile/features/disputes/screens/dispute_detail_screen.dart';
import 'package:freightlink_mobile/features/disputes/screens/my_disputes_screen.dart';
import 'package:mocktail/mocktail.dart';
import 'package:provider/provider.dart';

import '../../../helpers/fakes.dart';

final _resolvedDispute = Dispute(
  disputeId: 'dispute-1',
  tripId: 'trip-1',
  raisedByUserId: 'shipper-1',
  raisedByName: 'Nadeesha Fernando',
  raisedByRole: 'Shipper',
  tripRouteSummary: 'Colombo → Kandy',
  category: DisputeCategory.delay,
  description: 'The carrier arrived outside the documented pickup time window.',
  status: DisputeStatus.resolved,
  createdAt: DateTime.utc(2026, 9, 27, 9),
  updatedAt: DateTime.utc(2026, 9, 27, 12),
  resolution: DisputeResolution(
    disputeId: 'dispute-1',
    resolvedByUserId: 'admin-1',
    outcome: DisputeOutcome.partiallyUpheld,
    notes: 'The delay was verified and a partial credit was approved.',
    resolvedAt: DateTime.utc(2026, 9, 27, 12),
  ),
);

void main() {
  late MockDisputeRepository repository;

  setUpAll(() => registerFallbackValue(DisputeStatus.raised));
  setUp(() => repository = MockDisputeRepository());

  testWidgets(
    'shows role-scoped dispute data and refreshes when status filter changes',
    (tester) async {
      when(
        () => repository.getMyDisputes(status: any(named: 'status')),
      ).thenAnswer((_) async => [_resolvedDispute]);

      await tester.pumpWidget(
        Provider<DisputeRepository>.value(
          value: repository,
          child: MaterialApp(
            theme: AppTheme.light,
            home: const MyDisputesScreen(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Colombo → Kandy'), findsOneWidget);
    expect(find.text('Resolved'), findsNWidgets(2));
      expect(find.textContaining('Filed by Nadeesha Fernando'), findsOneWidget);

      await tester.tap(find.text('Resolved').first);
      await tester.pumpAndSettle();
      verify(
        () => repository.getMyDisputes(status: DisputeStatus.resolved),
      ).called(1);
    },
  );

  testWidgets(
    'shows the read-only Admin resolution and no claimant mutation action',
    (tester) async {
      when(
        () => repository.getDisputeDetail('dispute-1'),
      ).thenAnswer((_) async => _resolvedDispute);

      await tester.pumpWidget(
        Provider<DisputeRepository>.value(
          value: repository,
          child: MaterialApp(
            theme: AppTheme.light,
            home: const DisputeDetailScreen(disputeId: 'dispute-1'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('ADMIN RESOLUTION'), findsOneWidget);
      expect(find.text('Outcome: Partially Upheld'), findsOneWidget);
      expect(
        find.text('The delay was verified and a partial credit was approved.'),
        findsOneWidget,
      );
    expect(find.text('Submit dispute'), findsNothing);
    expect(find.text('Start Review'), findsNothing);
    },
  );
}
