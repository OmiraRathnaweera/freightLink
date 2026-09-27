import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/trips/models/trip_evidence.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/proof_of_delivery_screen.dart';
import 'package:freightlink_mobile/features/trips/services/image_capture_service.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockTripsRepository mockTripsRepo;
  late MockImageCaptureService mockCaptureService;

  final sampleInTransitTrip = TripResponse(
    tripId: 'c2000000-0000-0000-0000-000000000002',
    assignmentId: 'b2000000-0000-0000-0000-000000000002',
    loadId: 'a2000000-0000-0000-0000-000000000002',
    agencyId: '258466d2-3d76-46c5-9eb4-ede1f49224ba',
    agencyName: 'Samagi Express Logistics',
    vehicleId: 'ba200000-0000-0000-0000-000000000002',
    vehicleRegistrationNo: 'WP-DA-8920',
    driverId: 'd2000000-0000-0000-0000-000000000002',
    driverName: 'Kamal Fernando',
    status: 'InTransit',
    pickupAddress: 'Kelaniya Distribution Hub, Peliyagoda',
    dropoffAddress: 'Galle Port Warehouse Complex, Galle',
    cargoDescription: 'High-Capacity Solar Inverters & Batteries',
    weightKg: 2400.0,
    volumeM3: 10.0,
    referenceCode: 'LD-KLY-GAL-02',
    routedDistanceKm: 125.0,
    proposedEtaMinutes: 150,
    evidence: [
      TripEvidence(
        tripEvidenceId: 'ev-pickup-1',
        capturedByUserId: 'u-staff-1',
        evidenceType: 'PickupProof',
        storageKey: 'key-pickup-1',
        capturedAt: DateTime(2026, 9, 19, 11, 0),
      ),
    ],
    createdAt: DateTime(2026, 9, 19, 10, 0),
    updatedAt: DateTime(2026, 9, 19, 11, 30),
  );

  // 1x1 transparent PNG bytes for testing image preview
  final fakeImageBytes = Uint8List.fromList([
    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
    0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
    0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
    0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
    0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
    0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
    0x42, 0x60, 0x82
  ]);

  final fakeCapturedImage = CapturedImage(
    bytes: fakeImageBytes,
    name: 'delivery_manifest_test.jpg',
    mimeType: 'image/jpeg',
  );

  setUp(() {
    mockTripsRepo = MockTripsRepository();
    mockCaptureService = MockImageCaptureService();
  });

  testWidgets('ProofOfDeliveryScreen renders trip details, dropoff address, policy alert, and capture buttons',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: sampleInTransitTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Verify Title & Status
    expect(find.text('Proof of Delivery'), findsOneWidget);
    expect(find.text('INTRANSIT'), findsOneWidget);

    // Verify Trip Summary
    expect(find.text('TRIP #C2000000'), findsOneWidget);
    expect(find.text('Galle Port Warehouse Complex, Galle'), findsOneWidget);
    expect(find.text('High-Capacity Solar Inverters & Batteries'), findsOneWidget);
    expect(find.text('WP-DA-8920'), findsOneWidget);
    expect(find.text('Kamal Fernando'), findsOneWidget);

    // Verify Hard-Block Notice (Y3S01-74)
    expect(find.text('Evidence Hard-Block Policy (Y3S01-74)'), findsOneWidget);
    expect(
      find.textContaining('photo evidence of handover must be captured before this trip can advance to Delivered'),
      findsOneWidget,
    );

    // Verify Capture Actions
    expect(find.byKey(const Key('take_photo_button')), findsOneWidget);
    expect(find.byKey(const Key('pick_gallery_button')), findsOneWidget);
    expect(find.text('No photo captured yet'), findsOneWidget);
    expect(find.byKey(const Key('delivery_notes_field')), findsOneWidget);
    expect(find.byKey(const Key('submit_delivery_proof_button')), findsOneWidget);
  });

  testWidgets('ProofOfDeliveryScreen takes photo and displays preview and retake button',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.capturePhoto()).thenAnswer((_) async => fakeCapturedImage);

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: sampleInTransitTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Tap Take Photo
    await tester.tap(find.byKey(const Key('take_photo_button')));
    await tester.pumpAndSettle();

    // Verify preview and metadata
    expect(find.byKey(const Key('captured_image_preview')), findsOneWidget);
    expect(find.text('Ready for upload'), findsOneWidget);
    expect(find.text('delivery_manifest_test.jpg'), findsOneWidget);
    expect(find.byKey(const Key('retake_photo_button')), findsOneWidget);

    // Tap Retake Photo
    await tester.ensureVisible(find.byKey(const Key('retake_photo_button')));
    await tester.tap(find.byKey(const Key('retake_photo_button')));
    await tester.pumpAndSettle();

    expect(find.text('No photo captured yet'), findsOneWidget);
  });

  testWidgets('ProofOfDeliveryScreen picks photo from gallery', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.pickFromGallery()).thenAnswer((_) async => fakeCapturedImage);

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: sampleInTransitTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Tap Gallery button
    await tester.tap(find.byKey(const Key('pick_gallery_button')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('captured_image_preview')), findsOneWidget);
    expect(find.text('Ready for upload'), findsOneWidget);
  });

  testWidgets('ProofOfDeliveryScreen executes full upload, DeliveryProof evidence link, and status advance workflow',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.capturePhoto()).thenAnswer((_) async => fakeCapturedImage);

    const uploadResult = FileUploadResult(
      publicId: 'cld_delivery_proof_456',
      secureUrl: 'https://res.cloudinary.com/test/image/upload/cld_delivery_proof_456.jpg',
    );

    final createdEvidence = TripEvidence(
      tripEvidenceId: 'ev-del-456',
      capturedByUserId: 'd2000000-0000-0000-0000-000000000002',
      evidenceType: 'DeliveryProof',
      storageKey: 'cld_delivery_proof_456',
      secureUrl: 'https://res.cloudinary.com/test/image/upload/cld_delivery_proof_456.jpg',
      capturedAt: DateTime.now(),
    );

    final updatedTrip = TripResponse(
      tripId: sampleInTransitTrip.tripId,
      assignmentId: sampleInTransitTrip.assignmentId,
      loadId: sampleInTransitTrip.loadId,
      agencyId: sampleInTransitTrip.agencyId,
      agencyName: sampleInTransitTrip.agencyName,
      vehicleId: sampleInTransitTrip.vehicleId,
      vehicleRegistrationNo: sampleInTransitTrip.vehicleRegistrationNo,
      driverId: sampleInTransitTrip.driverId,
      driverName: sampleInTransitTrip.driverName,
      status: 'Delivered',
      pickupAddress: sampleInTransitTrip.pickupAddress,
      dropoffAddress: sampleInTransitTrip.dropoffAddress,
      cargoDescription: sampleInTransitTrip.cargoDescription,
      weightKg: sampleInTransitTrip.weightKg,
      volumeM3: sampleInTransitTrip.volumeM3,
      referenceCode: sampleInTransitTrip.referenceCode,
      evidence: [...sampleInTransitTrip.evidence, createdEvidence],
      createdAt: sampleInTransitTrip.createdAt,
      updatedAt: DateTime.now(),
    );

    when(() => mockTripsRepo.uploadFile(
          bytes: any(named: 'bytes'),
          filename: any(named: 'filename'),
        )).thenAnswer((_) async => uploadResult);

    when(() => mockTripsRepo.submitEvidence(
          tripId: any(named: 'tripId'),
          publicId: any(named: 'publicId'),
          evidenceType: any(named: 'evidenceType'),
          lat: any(named: 'lat'),
          lng: any(named: 'lng'),
        )).thenAnswer((_) async => createdEvidence);

    when(() => mockTripsRepo.changeTripStatus(
          tripId: any(named: 'tripId'),
          targetStatus: any(named: 'targetStatus'),
          notes: any(named: 'notes'),
          lat: any(named: 'lat'),
          lng: any(named: 'lng'),
        )).thenAnswer((_) async => updatedTrip);

    TripResponse? callbackTrip;

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: sampleInTransitTrip,
        imageCaptureService: mockCaptureService,
        onDeliveryConfirmed: (t) => callbackTrip = t,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Capture photo
    await tester.tap(find.byKey(const Key('take_photo_button')));
    await tester.pumpAndSettle();

    // Enter notes
    await tester.ensureVisible(find.byKey(const Key('delivery_notes_field')));
    await tester.enterText(
      find.byKey(const Key('delivery_notes_field')),
      'Signed by Receiving Officer Mr. Nimal, cargo in good order.',
    );
    await tester.pump();

    // Submit
    await tester.ensureVisible(find.byKey(const Key('submit_delivery_proof_button')));
    await tester.tap(find.byKey(const Key('submit_delivery_proof_button')));
    await tester.pump();
    await tester.pumpAndSettle();

    // Verify all 3 operations called in order
    verify(() => mockTripsRepo.uploadFile(
          bytes: fakeCapturedImage.bytes,
          filename: fakeCapturedImage.name,
        )).called(1);

    verify(() => mockTripsRepo.submitEvidence(
          tripId: sampleInTransitTrip.tripId,
          publicId: 'cld_delivery_proof_456',
          evidenceType: 'DeliveryProof',
        )).called(1);

    verify(() => mockTripsRepo.changeTripStatus(
          tripId: sampleInTransitTrip.tripId,
          targetStatus: 'Delivered',
          notes: 'Signed by Receiving Officer Mr. Nimal, cargo in good order.',
        )).called(1);

    expect(callbackTrip, isNotNull);
    expect(callbackTrip?.status, equals('Delivered'));

    // Success dialog
    expect(find.text('Delivery Confirmed!'), findsOneWidget);
    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
  });

  testWidgets('ProofOfDeliveryScreen shows error banner if upload fails', (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.capturePhoto()).thenAnswer((_) async => fakeCapturedImage);

    when(() => mockTripsRepo.uploadFile(
          bytes: any(named: 'bytes'),
          filename: any(named: 'filename'),
        )).thenThrow(
      const ApiException(
        statusCode: 400,
        code: 'INVALID_FILE_TYPE',
        message: 'The uploaded file format is not supported.',
      ),
    );

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: sampleInTransitTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Capture photo
    await tester.tap(find.byKey(const Key('take_photo_button')));
    await tester.pumpAndSettle();

    // Submit
    await tester.ensureVisible(find.byKey(const Key('submit_delivery_proof_button')));
    await tester.tap(find.byKey(const Key('submit_delivery_proof_button')));
    await tester.pumpAndSettle();

    expect(find.text('The uploaded file format is not supported.'), findsOneWidget);
  });

  testWidgets('ProofOfDeliveryScreen displays verified notice if delivery proof already exists',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final deliveredTrip = TripResponse(
      tripId: sampleInTransitTrip.tripId,
      assignmentId: sampleInTransitTrip.assignmentId,
      loadId: sampleInTransitTrip.loadId,
      agencyId: sampleInTransitTrip.agencyId,
      agencyName: sampleInTransitTrip.agencyName,
      vehicleId: sampleInTransitTrip.vehicleId,
      vehicleRegistrationNo: sampleInTransitTrip.vehicleRegistrationNo,
      driverId: sampleInTransitTrip.driverId,
      driverName: sampleInTransitTrip.driverName,
      status: 'Delivered',
      pickupAddress: sampleInTransitTrip.pickupAddress,
      dropoffAddress: sampleInTransitTrip.dropoffAddress,
      cargoDescription: sampleInTransitTrip.cargoDescription,
      weightKg: sampleInTransitTrip.weightKg,
      volumeM3: sampleInTransitTrip.volumeM3,
      referenceCode: sampleInTransitTrip.referenceCode,
      evidence: [
        TripEvidence(
          tripEvidenceId: 'ev-del-done',
          capturedByUserId: 'u-driver-1',
          evidenceType: 'DeliveryProof',
          storageKey: 'key-done',
          capturedAt: DateTime.now(),
        ),
      ],
      createdAt: sampleInTransitTrip.createdAt,
    );

    await pumpApp(
      tester,
      ProofOfDeliveryScreen(
        trip: deliveredTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    expect(find.text('DELIVERED'), findsOneWidget);
    expect(find.text('Proof of Delivery Verified'), findsOneWidget);
    expect(find.text('Delivery Evidence Verified'), findsOneWidget);
  });
}
