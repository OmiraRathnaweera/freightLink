import '../../../core/models/paged_result.dart';
import '../../../core/network/api_client.dart';
import '../models/fleet_resources.dart';
import '../models/job_proposal.dart';
import '../models/trip_evidence.dart';
import '../models/trip_models.dart';

/// Repository wrapping ApiClient for Trip execution, Assignment proposals,
/// and Fleet resource queries (Component C & B).
class TripsRepository {
  const TripsRepository(this._client);

  final ApiClient _client;

  /// Concurrency-safe dispatch endpoint (Y3S01-52): accepts assignment and creates trip.
  Future<TripResponse> createTrip(CreateTripRequest request) async {
    final json = await _client.post(
      '/trips',
      body: request.toJson(),
    ) as Map<String, dynamic>;
    return TripResponse.fromJson(json);
  }

  /// Lists proposals for the current agency staff's inbox (GET /api/v1/assignments).
  Future<PagedResult<JobProposal>> getProposals({
    ProposalStatus? status,
    int page = 1,
    int pageSize = 20,
    String? search,
  }) async {
    final query = <String, dynamic>{
      'page': page,
      'pageSize': pageSize,
      if (status != null && status != ProposalStatus.unknown)
        'status': status.displayName,
      if (search != null && search.isNotEmpty) 'search': search,
    };

    final json = await _client.get('/assignments', query: query) as Map<String, dynamic>;
    return PagedResult.fromJson(json, JobProposal.fromJson);
  }

  /// Fetches a single proposal's full details (GET /api/v1/assignments/{id}).
  Future<JobProposal> getProposalById(String assignmentId) async {
    final json = await _client.get('/assignments/$assignmentId') as Map<String, dynamic>;
    return JobProposal.fromJson(json);
  }

  /// Fetches agency fleet resources (available vehicles and active drivers).
  Future<({List<FleetVehicle> vehicles, List<FleetDriver> drivers})> getFleet({
    String? agencyId,
  }) async {
    final path = agencyId != null && agencyId.isNotEmpty
        ? '/agencies/$agencyId/fleet'
        : '/agencies/my/fleet';

    final json = await _client.get(path) as Map<String, dynamic>;
    final vList = (json['vehicles'] as List<dynamic>?)
            ?.map((v) => FleetVehicle.fromJson(v as Map<String, dynamic>))
            .toList() ??
        [];
    final dList = (json['drivers'] as List<dynamic>?)
            ?.map((d) => FleetDriver.fromJson(d as Map<String, dynamic>))
            .toList() ??
        [];

    return (vehicles: vList, drivers: dList);
  }

  /// Accepts a proposed load assignment (POST /api/v1/assignments/{loadId}/accept).
  Future<JobProposal> acceptProposal(
    String loadId, {
    String? vehicleId,
    String? driverId,
    String? notes,
  }) async {
    final body = <String, dynamic>{};
    if (vehicleId != null) body['vehicleId'] = vehicleId;
    if (driverId != null) body['driverId'] = driverId;
    if (notes != null && notes.isNotEmpty) body['notes'] = notes;

    final json = await _client.post(
      '/assignments/$loadId/accept',
      body: body.isNotEmpty ? body : null,
    ) as Map<String, dynamic>;
    return JobProposal.fromJson(json);
  }

  /// Declines a proposed load assignment (POST /api/v1/assignments/{loadId}/decline).
  Future<JobProposal> declineProposal(String loadId, {String? reason}) async {
    final json = await _client.post(
      '/assignments/$loadId/decline',
      body: reason != null && reason.isNotEmpty ? {'reason': reason} : null,
    ) as Map<String, dynamic>;
    return JobProposal.fromJson(json);
  }

  /// Uploads a photo/document file (POST /api/v1/files/single).
  Future<FileUploadResult> uploadFile({
    required List<int> bytes,
    required String filename,
  }) async {
    final json = await _client.postMultipart(
      '/files/single',
      fileBytes: bytes,
      filename: filename,
    ) as Map<String, dynamic>;
    return FileUploadResult.fromJson(json);
  }

  /// Submits proof-of-pickup or proof-of-delivery evidence (POST /api/v1/trips/{id}/evidence).
  Future<TripEvidence> submitEvidence({
    required String tripId,
    required String publicId,
    required String evidenceType,
    double? lat,
    double? lng,
  }) async {
    final body = <String, dynamic>{
      'publicId': publicId,
      'evidenceType': evidenceType,
      'capturedLat': ?lat,
      'capturedLng': ?lng,
    };

    final json = await _client.post(
      '/trips/$tripId/evidence',
      body: body,
    ) as Map<String, dynamic>;
    return TripEvidence.fromJson(json);
  }

  /// Advances a trip's status (POST /api/v1/trips/{id}/status).
  /// Hard-blocked if transitioning to PickedUp without PickupProof.
  Future<TripResponse> changeTripStatus({
    required String tripId,
    required String targetStatus,
    String? notes,
    double? lat,
    double? lng,
  }) async {
    final request = TripStatusChangeRequest(
      targetStatus: targetStatus,
      notes: notes,
      snapshotLat: lat,
      snapshotLng: lng,
    );

    final json = await _client.post(
      '/trips/$tripId/status',
      body: request.toJson(),
    ) as Map<String, dynamic>;
    return TripResponse.fromJson(json);
  }

  /// Retrieves a single trip's full detail including timeline & evidence (GET /api/v1/trips/{id}).
  Future<TripResponse> getTripById(String tripId) async {
    final json = await _client.get('/trips/$tripId') as Map<String, dynamic>;
    return TripResponse.fromJson(json);
  }

  /// Lists captured evidence for a trip (GET /api/v1/trips/{id}/evidence).
  Future<List<TripEvidence>> getTripEvidence(String tripId) async {
    final json = await _client.get('/trips/$tripId/evidence') as List<dynamic>;
    return json
        .map((e) => TripEvidence.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// Lists trips (for AgencyStaff / Driver / Admin) via GET /api/v1/trips.
  /// When called by a Driver, results are automatically scoped by the backend to that driver.
  Future<PagedResult<TripResponse>> getTrips({
    String? status,
    int page = 1,
    int pageSize = 20,
    String? sortBy,
    String? sortDir,
  }) async {
    final query = <String, dynamic>{
      'page': page,
      'pageSize': pageSize,
      if (status != null && status.isNotEmpty) 'status': status,
      if (sortBy != null && sortBy.isNotEmpty) 'sortBy': sortBy,
      if (sortDir != null && sortDir.isNotEmpty) 'sortDir': sortDir,
    };

    final json = await _client.get('/trips', query: query) as Map<String, dynamic>;
    return PagedResult.fromJson(json, TripResponse.fromJson);
  }

  /// Convenience method to fetch the driver's currently active assigned trip (if any).
  /// Queries the driver's trips, finds the first active trip (Assigned, PickedUp, InTransit),
  /// and fetches its full detail via [getTripById].
  Future<TripResponse?> getDriverActiveTrip() async {
    final paged = await getTrips(pageSize: 20, sortBy: 'createdAt', sortDir: 'desc');
    final active = paged.items.where((t) =>
      t.isAssigned || t.isPickedUp || t.isInTransit
    ).firstOrNull;

    if (active == null) return null;
    return getTripById(active.tripId);
  }
}
