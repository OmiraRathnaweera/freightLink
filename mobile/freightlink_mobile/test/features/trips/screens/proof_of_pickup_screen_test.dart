import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/core/network/api_exception.dart';
import 'package:freightlink_mobile/features/trips/models/trip_evidence.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/screens/proof_of_pickup_screen.dart';
import 'package:freightlink_mobile/features/trips/services/image_capture_service.dart';
import 'package:mocktail/mocktail.dart';

import '../../../helpers/fakes.dart';
import '../../../helpers/pump_app.dart';

void main() {
  setUpAll(registerFallbackValues);

  late MockTripsRepository mockTripsRepo;
  late MockImageCaptureService mockCaptureService;

  final sampleTrip = TripResponse(
    tripId: 'c1000000-0000-0000-0000-000000000001',
    assignmentId: 'b1000000-0000-0000-0000-000000000001',
    loadId: 'a1000000-0000-0000-0000-000000000001',
    agencyId: 'ba000000-0000-0000-0000-000000000001',
    agencyName: 'Samagi Express Logistics',
    vehicleId: 'ba100000-0000-0000-0000-000000000001',
    vehicleRegistrationNo: 'WP-CAB-4521',
    driverId: 'd1000000-0000-0000-0000-000000000001',
    driverName: 'Sunil Perera',
    status: 'Assigned',
    pickupAddress: 'Colombo Port Terminal 3, Colombo 01',
    dropoffAddress: 'Pallekele BOI Industrial Zone, Kandy',
    createdAt: DateTime(2026, 9, 19, 8, 30),
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
    name: 'pickup_manifest_test.jpg',
    mimeType: 'image/jpeg',
  );

  setUp(() {
    mockTripsRepo = MockTripsRepository();
    mockCaptureService = MockImageCaptureService();
  });

  testWidgets('ProofOfPickupScreen renders trip details, policy alert, and capture buttons',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await pumpApp(
      tester,
      ProofOfPickupScreen(
        trip: sampleTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Verify Title & Status
    expect(find.text('Proof of Pickup'), findsOneWidget);
    expect(find.text('ASSIGNED'), findsOneWidget);

    // Verify Trip Summary
    expect(find.text('TRIP #C1000000'), findsOneWidget);
    expect(find.text('Colombo Port Terminal 3, Colombo 01'), findsOneWidget);
    expect(find.text('Pallekele BOI Industrial Zone, Kandy'), findsOneWidget);
    expect(find.text('WP-CAB-4521'), findsOneWidget);
    expect(find.text('Sunil Perera'), findsOneWidget);

    // Verify Hard-Block Notice
    expect(find.text('Evidence Hard-Block Policy (Y3S01-74)'), findsOneWidget);
    expect(
      find.textContaining('photo evidence must be captured at the pickup site before this trip can advance to PickedUp'),
      findsOneWidget,
    );

    // Verify Capture Actions
    expect(find.byKey(const Key('take_photo_button')), findsOneWidget);
    expect(find.byKey(const Key('pick_gallery_button')), findsOneWidget);
    expect(find.text('No photo captured yet'), findsOneWidget);
  });

  testWidgets('ProofOfPickupScreen takes photo and displays preview and retake button',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.capturePhoto()).thenAnswer((_) async => fakeCapturedImage);

    await pumpApp(
      tester,
      ProofOfPickupScreen(
        trip: sampleTrip,
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
    expect(find.byKey(const Key('retake_photo_button')), findsOneWidget);

    // Tap Retake Photo
    await tester.ensureVisible(find.byKey(const Key('retake_photo_button')));
    await tester.tap(find.byKey(const Key('retake_photo_button')));
    await tester.pumpAndSettle();

    expect(find.text('No photo captured yet'), findsOneWidget);
  });

  testWidgets('ProofOfPickupScreen executes full upload, evidence link, and status advance workflow',
      (tester) async {
    tester.view.physicalSize = const Size(1080, 2400);
    tester.view.devicePixelRatio = 2.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    when(() => mockCaptureService.capturePhoto()).thenAnswer((_) async => fakeCapturedImage);

    const uploadResult = FileUploadResult(
      publicId: 'cld_evidence_sample_123',
      secureUrl: 'https://res.cloudinary.com/test/image/upload/cld_evidence_sample_123.jpg',
    );

    final createdEvidence = TripEvidence(
      tripEvidenceId: 'ev-123',
      capturedByUserId: 'u-agency-1',
      evidenceType: 'PickupProof',
      storageKey: 'cld_evidence_sample_123',
      secureUrl: 'https://res.cloudinary.com/test/image/upload/cld_evidence_sample_123.jpg',
      capturedAt: DateTime.now(),
    );

    final updatedTrip = TripResponse(
      tripId: sampleTrip.tripId,
      assignmentId: sampleTrip.assignmentId,
      loadId: sampleTrip.loadId,
      agencyId: sampleTrip.agencyId,
      agencyName: sampleTrip.agencyName,
      vehicleId: sampleTrip.vehicleId,
      vehicleRegistrationNo: sampleTrip.vehicleRegistrationNo,
      driverId: sampleTrip.driverId,
      driverName: sampleTrip.driverName,
      status: 'PickedUp',
      pickupAddress: sampleTrip.pickupAddress,
      dropoffAddress: sampleTrip.dropoffAddress,
      evidence: [createdEvidence],
      createdAt: sampleTrip.createdAt,
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
      ProofOfPickupScreen(
        trip: sampleTrip,
        imageCaptureService: mockCaptureService,
        onPickupConfirmed: (t) => callbackTrip = t,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Capture photo
    await tester.tap(find.byKey(const Key('take_photo_button')));
    await tester.pumpAndSettle();

    // Enter notes
    await tester.ensureVisible(find.byKey(const Key('pickup_notes_field')));
    await tester.enterText(
      find.byKey(const Key('pickup_notes_field')),
      '500 crates loaded securely, seal #FL-9912 verified.',
    );
    await tester.pump();

    // Submit
    await tester.ensureVisible(find.byKey(const Key('submit_pickup_proof_button')));
    await tester.tap(find.byKey(const Key('submit_pickup_proof_button')));
    await tester.pump();
    await tester.pumpAndSettle();

    // Verify all 3 operations called in order
    verify(() => mockTripsRepo.uploadFile(
          bytes: fakeCapturedImage.bytes,
          filename: fakeCapturedImage.name,
        )).called(1);

    verify(() => mockTripsRepo.submitEvidence(
          tripId: sampleTrip.tripId,
          publicId: 'cld_evidence_sample_123',
          evidenceType: 'PickupProof',
        )).called(1);

    verify(() => mockTripsRepo.changeTripStatus(
          tripId: sampleTrip.tripId,
          targetStatus: 'PickedUp',
          notes: '500 crates loaded securely, seal #FL-9912 verified.',
        )).called(1);

    expect(callbackTrip, isNotNull);
    expect(callbackTrip?.status, equals('PickedUp'));

    // Success dialog
    expect(find.text('Pickup Confirmed!'), findsOneWidget);
    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
  });

  testWidgets('ProofOfPickupScreen shows error banner if upload fails', (tester) async {
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
      ProofOfPickupScreen(
        trip: sampleTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    // Capture photo
    await tester.tap(find.byKey(const Key('take_photo_button')));
    await tester.pumpAndSettle();

    // Submit
    await tester.ensureVisible(find.byKey(const Key('submit_pickup_proof_button')));
    await tester.tap(find.byKey(const Key('submit_pickup_proof_button')));
    await tester.pumpAndSettle();

    expect(find.text('The uploaded file format is not supported.'), findsOneWidget);
  });

  testWidgets('ProofOfPickupScreen displays verified notice if evidence already exists',
      (tester) async {
    final pickedUpTrip = TripResponse(
      tripId: sampleTrip.tripId,
      assignmentId: sampleTrip.assignmentId,
      loadId: sampleTrip.loadId,
      agencyId: sampleTrip.agencyId,
      agencyName: sampleTrip.agencyName,
      vehicleId: sampleTrip.vehicleId,
      vehicleRegistrationNo: sampleTrip.vehicleRegistrationNo,
      driverId: sampleTrip.driverId,
      driverName: sampleTrip.driverName,
      status: 'PickedUp',
      pickupAddress: sampleTrip.pickupAddress,
      dropoffAddress: sampleTrip.dropoffAddress,
      evidence: [
        TripEvidence(
          tripEvidenceId: 'ev-existing',
          capturedByUserId: 'u-1',
          evidenceType: 'PickupProof',
          storageKey: 'key-1',
          capturedAt: DateTime.now(),
        ),
      ],
      createdAt: sampleTrip.createdAt,
    );

    await pumpApp(
      tester,
      ProofOfPickupScreen(
        trip: pickedUpTrip,
        imageCaptureService: mockCaptureService,
      ),
      tripsRepository: mockTripsRepo,
    );

    await tester.pumpAndSettle();

    expect(find.text('PICKED UP'), findsOneWidget);
    expect(find.text('Proof of Pickup Verified'), findsOneWidget);
  });
}
