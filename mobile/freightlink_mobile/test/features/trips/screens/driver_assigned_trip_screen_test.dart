import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/auth/models/auth_user.dart';
import 'package:freightlink_mobile/features/auth/providers/auth_provider.dart';
import 'package:freightlink_mobile/features/trips/models/trip_evidence.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/driver_assigned_trip_screen.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/pump_app.dart';

class MockAuthProvider extends Mock implements AuthProvider {}

void main() {
  setUpAll(registerFallbackValues);

  late MockTripsRepository mockTripsRepo;
  late MockAuthProvider mockAuthProvider;

  final sampleDriverUser = AuthUser(
    userId: '22222222-2222-2222-2222-222222222222',
    email: 'driver2@freightlink.lk',
    fullName: 'Kamal Fernando',
    role: 'Driver',
    isActive: true,
  );

  final sampleAssignedTrip = TripResponse(
    tripId: 'c2000000-0000-0000-0000-000000000002',
    assignmentId: 'b2000000-0000-0000-0000-000000000002',
    loadId: 'a2000000-0000-0000-0000-000000000002',
    agencyId: '258466d2-3d76-46c5-9eb4-ede1f49224ba',
    agencyName: 'Samagi Express Logistics',
    vehicleId: 'ba200000-0000-0000-0000-000000000002',
    vehicleRegistrationNo: 'WP-DA-8920',
    driverId: 'd2000000-0000-0000-0000-000000000002',
    driverName: 'Kamal Fernando',
    status: 'Assigned',
    pickupAddress: 'Kelaniya Distribution Hub, Peliyagoda',
    dropoffAddress: 'Galle Port Warehouse Complex, Galle',
    cargoDescription: 'High-Capacity Solar Inverters & Batteries',
    weightKg: 2400.0,
    volumeM3: 10.0,
    referenceCode: 'LD-KLY-GAL-02',
    routedDistanceKm: 125.0,
    proposedEtaMinutes: 150,
    createdAt: DateTime(2026, 9, 19, 10, 0),
  );

  final samplePickedUpTrip = TripResponse(
    tripId: sampleAssignedTrip.tripId,
    assignmentId: sampleAssignedTrip.assignmentId,
    loadId: sampleAssignedTrip.loadId,
    agencyId: sampleAssignedTrip.agencyId,
    agencyName: sampleAssignedTrip.agencyName,
    vehicleId: sampleAssignedTrip.vehicleId,
    vehicleRegistrationNo: sampleAssignedTrip.vehicleRegistrationNo,
    driverId: sampleAssignedTrip.driverId,
    driverName: sampleAssignedTrip.driverName,
    status: 'PickedUp',
    pickupAddress: sampleAssignedTrip.pickupAddress,
    dropoffAddress: sampleAssignedTrip.dropoffAddress,
    cargoDescription: sampleAssignedTrip.cargoDescription,
    weightKg: sampleAssignedTrip.weightKg,
    volumeM3: sampleAssignedTrip.volumeM3,
    referenceCode: sampleAssignedTrip.referenceCode,
    routedDistanceKm: sampleAssignedTrip.routedDistanceKm,
    proposedEtaMinutes: sampleAssignedTrip.proposedEtaMinutes,
    evidence: [
      TripEvidence(
        tripEvidenceId: 'e-1',
        capturedByUserId: 'u-staff-1',
        evidenceType: 'PickupProof',
        storageKey: 'key-1',
        capturedAt: DateTime(2026, 9, 19, 11, 0),
      ),
    ],
    createdAt: sampleAssignedTrip.createdAt,
    updatedAt: DateTime(2026, 9, 19, 11, 0),
  );

  final sampleInTransitTrip = TripResponse(
    tripId: samplePickedUpTrip.tripId,
    assignmentId: samplePickedUpTrip.assignmentId,
    loadId: samplePickedUpTrip.loadId,
    agencyId: samplePickedUpTrip.agencyId,
    agencyName: samplePickedUpTrip.agencyName,
    vehicleId: samplePickedUpTrip.vehicleId,
    vehicleRegistrationNo: samplePickedUpTrip.vehicleRegistrationNo,
    driverId: samplePickedUpTrip.driverId,
    driverName: samplePickedUpTrip.driverName,
    status: 'InTransit',
    pickupAddress: samplePickedUpTrip.pickupAddress,
    dropoffAddress: samplePickedUpTrip.dropoffAddress,
    cargoDescription: samplePickedUpTrip.cargoDescription,
    weightKg: samplePickedUpTrip.weightKg,
    volumeM3: samplePickedUpTrip.volumeM3,
    referenceCode: samplePickedUpTrip.referenceCode,
    routedDistanceKm: samplePickedUpTrip.routedDistanceKm,
    proposedEtaMinutes: samplePickedUpTrip.proposedEtaMinutes,
    evidence: samplePickedUpTrip.evidence,
    createdAt: samplePickedUpTrip.createdAt,
    updatedAt: DateTime(2026, 9, 19, 11, 30),
  );

  setUp(() {
    mockTripsRepo = MockTripsRepository();
    mockAuthProvider = MockAuthProvider();

    when(() => mockAuthProvider.user).thenReturn(sampleDriverUser);
    when(() => mockAuthProvider.status).thenReturn(AuthStatus.authenticated);
  });

  testWidgets('DriverAssignedTripScreen shows loading indicator while fetching', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final completer = Completer<TripResponse?>();
    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) => completer.future);

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    // Initial pump without settling
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    completer.complete(null);
    await tester.pumpAndSettle();
  });

  testWidgets('DriverAssignedTripScreen displays empty card when no trip is assigned',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => null);

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(enableAutoPolling: false),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    expect(find.byKey(const Key('empty_trip_card')), findsOneWidget);
    expect(find.text('No Active Trip Assigned'), findsOneWidget);
    expect(find.byKey(const Key('check_assignments_button')), findsOneWidget);
  });

  testWidgets('DriverAssignedTripScreen displays active trip details (route, vehicle, cargo, status)',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => sampleAssignedTrip);

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    // Verify Header & Status
    expect(find.byKey(const Key('active_trip_view')), findsOneWidget);
    expect(find.byKey(const Key('new_assignment_banner')), findsOneWidget);
    expect(find.text('New Assigned Trip (Post-Approval)'), findsOneWidget);
    expect(find.byKey(const Key('assigned_status_card')), findsOneWidget);
    expect(find.text('Awaiting Pickup Verification'), findsOneWidget);
    expect(find.text('TRIP #C2000000'), findsOneWidget);
    expect(find.text('ASSIGNED'), findsOneWidget);
    expect(find.text('Load Ref: LD-KLY-GAL-02'), findsOneWidget);
    expect(find.text('125.0 km'), findsOneWidget);
    expect(find.text('2h 30m'), findsOneWidget);

    // Verify Route
    expect(find.text('Kelaniya Distribution Hub, Peliyagoda'), findsOneWidget);
    expect(find.text('Galle Port Warehouse Complex, Galle'), findsOneWidget);

    // Verify Vehicle & Cargo
    expect(find.text('WP-DA-8920'), findsOneWidget);
    expect(find.text('Samagi Express Logistics'), findsOneWidget);
    expect(find.text('High-Capacity Solar Inverters & Batteries'), findsOneWidget);
    expect(find.text('2400 kg • 10.0 m³'), findsOneWidget);

    // Verify Policy
    expect(find.text('Pickup Verification Pending'), findsOneWidget);
  });

  testWidgets('DriverAssignedTripScreen allows driver to advance status from PickedUp to InTransit',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => samplePickedUpTrip);
    when(() => mockTripsRepo.changeTripStatus(
          tripId: samplePickedUpTrip.tripId,
          targetStatus: 'InTransit',
          notes: any(named: 'notes'),
        )).thenAnswer((_) async => sampleInTransitTrip);

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    expect(find.text('PICKED UP'), findsOneWidget);
    expect(find.text('Proof of Pickup Verified'), findsOneWidget);
    expect(find.byKey(const Key('start_transit_button')), findsOneWidget);

    // Tap Start Transit
    await tester.ensureVisible(find.byKey(const Key('start_transit_button')));
    await tester.tap(find.byKey(const Key('start_transit_button')));
    await tester.pumpAndSettle();

    verify(() => mockTripsRepo.changeTripStatus(
          tripId: samplePickedUpTrip.tripId,
          targetStatus: 'InTransit',
          notes: any(named: 'notes'),
        )).called(1);

    expect(find.text('IN TRANSIT'), findsOneWidget);
    expect(find.text('Trip In Transit'), findsOneWidget);
  });

  testWidgets('DriverAssignedTripScreen displays error banner on failure with try again button',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockTripsRepo.getDriverActiveTrip()).thenThrow(
      const ApiException(
        statusCode: 500,
        code: 'INTERNAL_ERROR',
        message: 'Failed to retrieve active trip.',
      ),
    );

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(enableAutoPolling: false),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    expect(find.text('Failed to retrieve active trip.'), findsOneWidget);
    expect(find.text('Try Again'), findsOneWidget);
  });

  testWidgets('DriverAssignedTripScreen displays capture delivery proof button when InTransit',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async => sampleInTransitTrip);

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(enableAutoPolling: false),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    expect(find.text('IN TRANSIT'), findsOneWidget);
    expect(find.text('Trip In Transit'), findsOneWidget);
    expect(find.byKey(const Key('capture_delivery_proof_button')), findsOneWidget);
    expect(find.text('Complete Delivery & Capture Proof'), findsOneWidget);
  });

  testWidgets(
      'DriverAssignedTripScreen updates from empty state to assigned trip when Check for Assignments is tapped (Y3S01-99)',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    var fetchCount = 0;
    when(() => mockTripsRepo.getDriverActiveTrip()).thenAnswer((_) async {
      fetchCount++;
      // First call (initial): no trip assigned yet
      if (fetchCount == 1) return null;
      // Subsequent call (post-approval): newly assigned trip appears!
      return sampleAssignedTrip;
    });

    await pumpApp(
      tester,
      const DriverAssignedTripScreen(enableAutoPolling: false),
      tripsRepository: mockTripsRepo,
      authProvider: mockAuthProvider,
    );

    await tester.pumpAndSettle();

    // Verify initial empty state while waiting for Admin approval
    expect(find.byKey(const Key('empty_trip_card')), findsOneWidget);
    expect(find.text('No Active Trip Assigned'), findsOneWidget);
    expect(find.byKey(const Key('check_assignments_button')), findsOneWidget);
    expect(find.byKey(const Key('active_trip_view')), findsNothing);

    // Admin approves in React console -> Driver taps 'Check for Assignments' in Flutter
    await tester.tap(find.byKey(const Key('check_assignments_button')));
    await tester.pumpAndSettle();

    // Verify newly assigned trip appears post-approval
    expect(find.byKey(const Key('empty_trip_card')), findsNothing);
    expect(find.byKey(const Key('active_trip_view')), findsOneWidget);
    expect(find.byKey(const Key('new_assignment_banner')), findsOneWidget);
    expect(find.text('New Assigned Trip (Post-Approval)'), findsOneWidget);
    expect(find.text('ASSIGNED'), findsOneWidget);
    expect(find.text('Kelaniya Distribution Hub, Peliyagoda'), findsOneWidget);
    expect(find.text('Galle Port Warehouse Complex, Galle'), findsOneWidget);
    expect(find.text('WP-DA-8920'), findsOneWidget);
  });
}
