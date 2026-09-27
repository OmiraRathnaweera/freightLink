import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/features/trips/models/fleet_resources.dart';
import 'package:freightlink_mobile/features/trips/models/job_proposal.dart';
import 'package:freightlink_mobile/features/trips/screens/job_proposals_screen.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockTripsRepository mockTripsRepo;

  final sampleProposal1 = JobProposal(
    assignmentId: 'b4000000-0000-0000-0000-000000000004',
    loadId: 'a4000000-0000-0000-0000-000000000004',
    agencyId: 'ba000000-0000-0000-0000-000000000001',
    agencyName: 'WP Express Logistics',
    proposedPrice: 78500.0,
    routedDistanceKm: 395.5,
    proposedEtaMinutes: 420,
    status: ProposalStatus.proposed,
    cargoDescription: 'Fresh Northern Agricultural Produce & Dry Goods',
    weightKg: 4200.0,
    volumeM3: 18.0,
    pickupAddress: 'Jaffna Central Wholesale Market, Jaffna',
    dropoffAddress: 'Manning Market Wholesale Complex, Peliyagoda',
    pickupWindowStart: DateTime(2026, 9, 20, 8, 0),
    pickupWindowEnd: DateTime(2026, 9, 21, 18, 0),
    shipperName: 'Northern Agro Farmers Co.',
    referenceCode: 'LD-JAF-CMB-04',
    createdAt: DateTime(2026, 9, 19, 10, 0),
  );

  final sampleProposal2 = JobProposal(
    assignmentId: 'b4000000-0000-0000-0000-000000000005',
    loadId: 'a4000000-0000-0000-0000-000000000005',
    agencyId: 'ba000000-0000-0000-0000-000000000001',
    agencyName: 'WP Express Logistics',
    proposedPrice: 112000.0,
    routedDistanceKm: 280.0,
    proposedEtaMinutes: 300,
    status: ProposalStatus.accepted,
    cargoDescription: 'Export Ready Ceylon Tea Crates',
    weightKg: 8500.0,
    volumeM3: 32.0,
    pickupAddress: 'Kandy Tea Processing Hub, Peradeniya',
    dropoffAddress: 'Colombo Port Container Terminal, Gate 4',
    pickupWindowStart: DateTime(2026, 9, 22, 6, 0),
    pickupWindowEnd: DateTime(2026, 9, 22, 14, 0),
    shipperName: 'Ceylon Tea Exporters PLC',
    referenceCode: 'LD-KDY-COL-05',
    createdAt: DateTime(2026, 9, 19, 11, 0),
  );

  setUp(() {
    mockTripsRepo = MockTripsRepository();
    when(() => mockTripsRepo.getFleet(agencyId: any(named: 'agencyId')))
        .thenAnswer((_) async => (
              vehicles: <FleetVehicle>[],
              drivers: <FleetDriver>[],
            ));
  });

  testWidgets('JobProposalsScreen renders list of proposed assignments', (tester) async {
    when(() => mockTripsRepo.getProposals(
          status: any(named: 'status'),
          page: any(named: 'page'),
          pageSize: any(named: 'pageSize'),
        )).thenAnswer((_) async => PagedResult<JobProposal>(
          items: [sampleProposal1],
          page: 1,
          pageSize: 50,
          totalItems: 1,
          totalPages: 1,
        ));

    await pumpApp(
      tester,
      const JobProposalsScreen(),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    expect(find.text('Job Proposals'), findsOneWidget);
    expect(find.text('Fresh Northern Agricultural Produce & Dry Goods'), findsOneWidget);
    expect(find.textContaining('78,500.00'), findsOneWidget);
    expect(find.text('LD-JAF-CMB-04'), findsOneWidget);
    expect(find.text('Review & Assign Fleet'), findsOneWidget);
  });

  testWidgets('JobProposalsScreen handles filter chip switching', (tester) async {
    when(() => mockTripsRepo.getProposals(
          status: ProposalStatus.proposed,
          pageSize: 50,
        )).thenAnswer((_) async => PagedResult<JobProposal>(
          items: [sampleProposal1],
          page: 1,
          pageSize: 50,
          totalItems: 1,
          totalPages: 1,
        ));

    when(() => mockTripsRepo.getProposals(
          status: ProposalStatus.accepted,
          pageSize: 50,
        )).thenAnswer((_) async => PagedResult<JobProposal>(
          items: [sampleProposal2],
          page: 1,
          pageSize: 50,
          totalItems: 1,
          totalPages: 1,
        ));

    await pumpApp(
      tester,
      const JobProposalsScreen(),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();
    expect(find.text('Fresh Northern Agricultural Produce & Dry Goods'), findsOneWidget);

    // Switch to Accepted filter
    await tester.tap(find.text('Accepted'));
    await tester.pumpAndSettle();

    expect(find.text('Export Ready Ceylon Tea Crates'), findsOneWidget);
    expect(find.textContaining('112,000.00'), findsOneWidget);
  });

  testWidgets('JobProposalsScreen displays empty state when no proposals exist',
      (tester) async {
    when(() => mockTripsRepo.getProposals(
          status: any(named: 'status'),
          page: any(named: 'page'),
          pageSize: any(named: 'pageSize'),
        )).thenAnswer((_) async => const PagedResult<JobProposal>(
          items: [],
          page: 1,
          pageSize: 50,
          totalItems: 0,
          totalPages: 0,
        ));

    await pumpApp(
      tester,
      const JobProposalsScreen(),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    expect(find.text('No Job Proposals'), findsOneWidget);
    expect(
      find.text('No pending proposals right now. Newly approved loads will appear here.'),
      findsOneWidget,
    );
  });
}
