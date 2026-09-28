import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/loads/widgets/route_map_preview.dart';
import 'package:freightlink_mobile/features/trips/models/fleet_resources.dart';
import 'package:freightlink_mobile/features/trips/models/job_proposal.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/accept_load_assign_screen.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockTripsRepository mockTripsRepo;

  final sampleProposal = JobProposal(
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

  final sampleVehicles = [
    const FleetVehicle(
      vehicleId: 'v-1',
      agencyId: 'agency-1',
      registrationNo: 'WP-CAB-4521',
      vehicleType: 'Lorry',
      capacityKg: 6500.0,
      volumeM3: 30.0,
      status: 'Available',
      isAvailable: true,
    ),
    const FleetVehicle(
      vehicleId: 'v-2',
      agencyId: 'agency-1',
      registrationNo: 'WP-DA-8920',
      vehicleType: 'Container',
      capacityKg: 18000.0,
      volumeM3: 65.0,
      status: 'Available',
      isAvailable: true,
    ),
  ];

  final sampleDrivers = [
    const FleetDriver(
      driverId: 'd-1',
      userId: 'u-1',
      agencyId: 'agency-1',
      fullName: 'Sunil Perera',
      email: 'sunil@example.com',
      licenceNo: 'B-5512940',
      status: 'Active',
      isActive: true,
    ),
    const FleetDriver(
      driverId: 'd-2',
      userId: 'u-2',
      agencyId: 'agency-1',
      fullName: 'Kamal Silva',
      email: 'kamal@example.com',
      licenceNo: 'B-9023415',
      status: 'Active',
      isActive: true,
    ),
  ];

  setUp(() {
    mockTripsRepo = MockTripsRepository();
  });

  testWidgets('AcceptLoadAssignScreen renders proposal details and fleet selectors',
      (tester) async {
    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: sampleProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Verify Title & Status
    expect(find.text('Accept & Assign Load'), findsOneWidget);
    expect(find.text('PROPOSED'), findsOneWidget);

    // Verify Payout & Routing
    expect(find.textContaining('78,500.00'), findsOneWidget);
    expect(find.textContaining('395.5 km'), findsOneWidget);
    expect(find.textContaining('7h transit'), findsOneWidget);

    // Verify Route Map Preview
    expect(find.byType(RouteMapPreview), findsOneWidget);

    // Verify Cargo & Address
    expect(find.text('Fresh Northern Agricultural Produce & Dry Goods'), findsOneWidget);
    expect(find.text('Jaffna Central Wholesale Market, Jaffna'), findsOneWidget);
    expect(find.text('Manning Market Wholesale Complex, Peliyagoda'), findsOneWidget);

    // Verify Dropdowns
    expect(find.byKey(const Key('vehicle_dropdown')), findsOneWidget);
    expect(find.byKey(const Key('driver_dropdown')), findsOneWidget);

    // Verify Action Buttons
    expect(find.byKey(const Key('accept_assign_button')), findsOneWidget);
    expect(find.byKey(const Key('decline_button')), findsOneWidget);
  });

  testWidgets('AcceptLoadAssignScreen dispatches trip when submitted', (tester) async {
    final createdTrip = TripResponse(
      tripId: 'trip-new-123',
      assignmentId: sampleProposal.assignmentId,
      loadId: sampleProposal.loadId,
      agencyId: sampleProposal.agencyId,
      vehicleId: 'v-1',
      driverId: 'd-1',
      status: 'Assigned',
      createdAt: DateTime.now(),
    );

    when(() => mockTripsRepo.createTrip(any())).thenAnswer((_) async => createdTrip);

    TripResponse? callbackTrip;

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: sampleProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
        onTripCreated: (trip) => callbackTrip = trip,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Enter notes
    await tester.enterText(
      find.byType(TextField).last,
      'Fragile crates - handle with care',
    );
    await tester.pump();

    // Tap Accept & Assign
    await tester.tap(find.byKey(const Key('accept_assign_button')));
    await tester.pump();
    await tester.pumpAndSettle();

    // Verify API call
    verify(() => mockTripsRepo.createTrip(any(
          that: isA<CreateTripRequest>()
              .having((r) => r.assignmentId, 'assignmentId', sampleProposal.assignmentId)
              .having((r) => r.vehicleId, 'vehicleId', 'v-1')
              .having((r) => r.driverId, 'driverId', 'd-1')
              .having((r) => r.notes, 'notes', 'Fragile crates - handle with care'),
        ))).called(1);

    expect(callbackTrip, isNotNull);
    expect(callbackTrip?.tripId, equals('trip-new-123'));

    // Verify success modal
    expect(find.text('Trip Dispatched!'), findsOneWidget);
    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
  });

  testWidgets('AcceptLoadAssignScreen displays 409 conflict error banner on race condition',
      (tester) async {
    when(() => mockTripsRepo.createTrip(any())).thenThrow(
      const ApiException(
        statusCode: 409,
        code: 'TRIP_ALREADY_EXISTS',
        message: 'A trip has already been created for this assignment.',
      ),
    );

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: sampleProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Submit
    await tester.tap(find.byKey(const Key('accept_assign_button')));
    await tester.pumpAndSettle();

    // Error banner should appear
    expect(find.text('A trip has already been created for this assignment.'), findsOneWidget);
  });

  testWidgets('AcceptLoadAssignScreen allows declining proposal with reason',
      (tester) async {
    when(() => mockTripsRepo.declineProposal(any(), reason: any(named: 'reason')))
        .thenAnswer((_) async => sampleProposal);

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: sampleProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Tap Decline
    await tester.tap(find.byKey(const Key('decline_button')));
    await tester.pumpAndSettle();

    // Dialog appears
    expect(find.text('Decline Job Proposal?'), findsOneWidget);

    // Fill decline reason
    await tester.enterText(
      find.widgetWithText(TextField, 'Reason for decline (optional)'),
      'Fleet fully occupied with prior haul',
    );
    await tester.pump();

    // Tap Confirm Decline
    await tester.tap(find.text('Confirm Decline'));
    await tester.pumpAndSettle();

    verify(() => mockTripsRepo.declineProposal(
          sampleProposal.loadId,
          reason: 'Fleet fully occupied with prior haul',
        )).called(1);
  });

  testWidgets('AcceptLoadAssignScreen renders read-only state when proposal is already Accepted',
      (tester) async {
    final acceptedProposal = JobProposal(
      assignmentId: 'b4000000-0000-0000-0000-000000000004',
      loadId: 'a4000000-0000-0000-0000-000000000004',
      agencyId: 'ba000000-0000-0000-0000-000000000001',
      agencyName: 'WP Express Logistics',
      proposedPrice: 78500.0,
      routedDistanceKm: 395.5,
      proposedEtaMinutes: 420,
      status: ProposalStatus.accepted,
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
      respondedAt: DateTime(2026, 9, 19, 11, 30),
    );

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: acceptedProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Verify Title and Banner
    expect(find.text('Job Proposal (Accepted)'), findsOneWidget);
    expect(find.text('Job Proposal Accepted'), findsOneWidget);

    // Verify Action buttons are hidden, replaced by Back button
    expect(find.byKey(const Key('accept_assign_button')), findsNothing);
    expect(find.byKey(const Key('decline_button')), findsNothing);
    expect(find.byKey(const Key('back_button')), findsOneWidget);
    expect(find.text('Back to Trips'), findsOneWidget);
  });

  testWidgets('AcceptLoadAssignScreen renders read-only state with reason when proposal is already Declined',
      (tester) async {
    final declinedProposal = JobProposal(
      assignmentId: 'b4000000-0000-0000-0000-000000000004',
      loadId: 'a4000000-0000-0000-0000-000000000004',
      agencyId: 'ba000000-0000-0000-0000-000000000001',
      agencyName: 'WP Express Logistics',
      proposedPrice: 78500.0,
      routedDistanceKm: 395.5,
      proposedEtaMinutes: 420,
      status: ProposalStatus.declined,
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
      respondedAt: DateTime(2026, 9, 19, 11, 45),
      declineReason: 'No refrigerated lorry available this week',
    );

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: declinedProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Verify Title, Banner and Reason
    expect(find.text('Job Proposal (Declined)'), findsOneWidget);
    expect(find.text('Job Proposal Declined'), findsOneWidget);
    expect(find.text('Reason: "No refrigerated lorry available this week"'), findsOneWidget);

    // Verify ADR-018 re-assignment notice
    expect(
      find.text('In accordance with ADR-018, this load was returned to the matching engine for automatic re-assignment to the next qualified agency.'),
      findsOneWidget,
    );

    // Verify Action buttons are hidden, replaced by Back button
    expect(find.byKey(const Key('accept_assign_button')), findsNothing);
    expect(find.byKey(const Key('decline_button')), findsNothing);
    expect(find.byKey(const Key('back_button')), findsOneWidget);
    expect(find.text('Back to Proposals'), findsOneWidget);
  });

  testWidgets('AcceptLoadAssignScreen displays error banner when declining fails',
      (tester) async {
    when(() => mockTripsRepo.declineProposal(any(), reason: any(named: 'reason'))).thenThrow(
      const ApiException(
        statusCode: 409,
        code: 'PROPOSAL_CONFLICT',
        message: 'This proposal has already expired or been cancelled.',
      ),
    );

    await pumpApp(
      tester,
      AcceptLoadAssignScreen(
        proposal: sampleProposal,
        initialVehicles: sampleVehicles,
        initialDrivers: sampleDrivers,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Tap Decline
    await tester.tap(find.byKey(const Key('decline_button')));
    await tester.pumpAndSettle();

    // Tap Confirm Decline
    await tester.tap(find.text('Confirm Decline'));
    await tester.pumpAndSettle();

    // Error banner should appear
    expect(find.text('This proposal has already expired or been cancelled.'), findsOneWidget);
  });
}

