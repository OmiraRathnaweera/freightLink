import 'package:freightlink_mobile/core/storage/token_storage.dart';
import 'package:freightlink_mobile/features/loads/data/loads_repository.dart';
import 'package:freightlink_mobile/features/trips/data/trips_repository.dart';
import 'package:freightlink_mobile/features/trips/models/trip_models.dart';
import 'package:freightlink_mobile/features/trips/services/image_capture_service.dart';
import 'package:mocktail/mocktail.dart';

/// Mocktail fakes shared across the test suite. `LoadsRepository`, `TripsRepository`
/// and `TokenStorage` are all plain, non-final classes, so `Mock` can implement
/// them directly — no code generation, no need to touch real network/secure
/// storage in any test.
class MockLoadsRepository extends Mock implements LoadsRepository {}

class MockTripsRepository extends Mock implements TripsRepository {}

class MockImageCaptureService extends Mock implements ImageCaptureService {}

class MockTokenStorage extends Mock implements TokenStorage {}

final _fallbackDate = DateTime.utc(2026, 1, 1);

/// Registers fallback values for argument types used with `any()` matchers.
/// Must run once before any `when(...)`/`verify(...)` call that uses `any()`
/// for these types (mocktail throws otherwise).
void registerFallbackValues() {
  registerFallbackValue(
    LoadFormData(
      cargoDescription: 'fallback',
      weightKg: 1,
      volumeM3: 1,
      pickupAddress: 'fallback pickup',
      pickupLat: 0,
      pickupLng: 0,
      dropoffAddress: 'fallback dropoff',
      dropoffLat: 1,
      dropoffLng: 1,
      pickupWindowStart: _fallbackDate,
      pickupWindowEnd: _fallbackDate,
    ),
  );
  registerFallbackValue(
    const CreateTripRequest(
      assignmentId: 'fallback-assignment',
      vehicleId: 'fallback-vehicle',
      driverId: 'fallback-driver',
    ),
  );
}
