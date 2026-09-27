import '../../../core/models/paged_result.dart';
import '../../../core/network/api_client.dart';
import '../models/load.dart';
import '../models/load_status.dart';

/// Everything needed to submit a create/update — shared by Post a Load and
/// Edit Load (`CreateLoadDto`/`UpdateLoadDto` have the same field set, minus
/// `postImmediately` which only `CreateLoadDto` has).
class LoadFormData {
  const LoadFormData({
    required this.cargoDescription,
    required this.weightKg,
    required this.volumeM3,
    required this.pickupAddress,
    required this.pickupLat,
    required this.pickupLng,
    required this.dropoffAddress,
    required this.dropoffLat,
    required this.dropoffLng,
    required this.pickupWindowStart,
    required this.pickupWindowEnd,
  });

  final String cargoDescription;
  final double weightKg;
  final double volumeM3;
  final String pickupAddress;
  final double pickupLat;
  final double pickupLng;
  final String dropoffAddress;
  final double dropoffLat;
  final double dropoffLng;
  final DateTime pickupWindowStart;
  final DateTime pickupWindowEnd;

  Map<String, dynamic> toJson() => {
    'cargoDescription': cargoDescription,
    'weightKg': weightKg,
    'volumeM3': volumeM3,
    'pickupAddress': pickupAddress,
    'pickupLat': pickupLat,
    'pickupLng': pickupLng,
    'dropoffAddress': dropoffAddress,
    'dropoffLat': dropoffLat,
    'dropoffLng': dropoffLng,
    'pickupWindowStart': pickupWindowStart.toUtc().toIso8601String(),
    'pickupWindowEnd': pickupWindowEnd.toUtc().toIso8601String(),
  };
}

/// Wraps [ApiClient] with one method per `LoadsController` action. Providers
/// depend on this rather than on `ApiClient` directly, so a future change to
/// the backend's shape only touches this file.
class LoadsRepository {
  const LoadsRepository(this._client);

  final ApiClient _client;

  Future<Load> create(LoadFormData data, {bool postImmediately = false}) async {
    final json =
        await _client.post(
              '/loads',
              body: {...data.toJson(), 'postImmediately': postImmediately},
            )
            as Map<String, dynamic>;
    return Load.fromJson(json);
  }

  Future<Load> getById(String loadId) async {
    final json = await _client.get('/loads/$loadId') as Map<String, dynamic>;
    return Load.fromJson(json);
  }

  Future<PagedResult<LoadListItem>> getList({
    int page = 1,
    int pageSize = 20,
    String? search,
    LoadStatus? status,
    String sortBy = 'createdAt',
    String sortDir = 'desc',
  }) async {
    final json =
        await _client.get(
              '/loads',
              query: {
                'page': page,
                'pageSize': pageSize,
                'sortBy': sortBy,
                'sortDir': sortDir,
                if (search != null && search.isNotEmpty) 'search': search,
                if (status != null) 'status': status.wireName,
              },
            )
            as Map<String, dynamic>;
    return PagedResult.fromJson(json, LoadListItem.fromJson);
  }

  Future<Load> update(String loadId, LoadFormData data) async {
    final json =
        await _client.put('/loads/$loadId', body: data.toJson())
            as Map<String, dynamic>;
    return Load.fromJson(json);
  }

  Future<String> uploadFile(List<int> bytes, String filename) async {
    final response = await _client.postMultipart(
      '/files/single',
      fileBytes: bytes,
      filename: filename,
    ) as Map<String, dynamic>;
    return response['publicId'] as String;
  }

  Future<void> attachFile(String loadId, String publicId, String fileType) async {
    await _client.post('/loads/$loadId/files', body: {
      'publicId': publicId,
      'fileType': fileType,
    });
  }

  Future<Load> post(String loadId) => _changeStatus(loadId, LoadStatus.posted);

  Future<Load> cancel(String loadId, {required String reason}) =>
      _changeStatus(loadId, LoadStatus.cancelled, reason: reason);

  Future<Load> _changeStatus(
    String loadId,
    LoadStatus status, {
    String? reason,
  }) async {
    final json =
        await _client.patch(
              '/loads/$loadId/status',
              body: {'status': status.wireName, 'reason': ?reason},
            )
            as Map<String, dynamic>;
    return Load.fromJson(json);
  }
}
