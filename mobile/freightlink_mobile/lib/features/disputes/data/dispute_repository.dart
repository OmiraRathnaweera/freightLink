import '../../../core/network/api_client.dart';
import '../models/dispute.dart';

/// API boundary for the claimant-facing dispute flow. The backend scopes list
/// and detail responses to the caller's related trips, so this repository
/// does not duplicate permission decisions in Flutter.
class DisputeRepository {
  DisputeRepository(this._client);

  final ApiClient _client;

  Future<Dispute> raiseDispute({
    required String tripId,
    required DisputeCategory category,
    required String description,
  }) async {
    final response =
        await _client.post(
              '/disputes',
              body: {
                'tripId': tripId,
                'category': category.wireName,
                'description': description.trim(),
              },
            )
            as Map<String, dynamic>;
    return Dispute.fromJson(response);
  }

  Future<List<Dispute>> getMyDisputes({DisputeStatus? status}) async {
    final response =
        await _client.get(
              '/disputes',
              query: {
                'page': 1,
                'pageSize': 100,
                if (status != null) 'status': status.wireName,
              },
            )
            as Map<String, dynamic>;
    return (response['items'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Dispute.fromJson)
        .toList();
  }

  Future<Dispute> getDisputeDetail(String disputeId) async {
    final response =
        await _client.get('/disputes/$disputeId') as Map<String, dynamic>;
    return Dispute.fromJson(response);
  }
}
