/// One status-transition row in a trip's append-only audit trail
/// (`GET /api/v1/trips/{id}`'s `events` array). Mirrors the backend's
/// `TripEventResponseDto` and the web app's timeline entries exactly, so the
/// mobile trip detail screen can show the same status/timeline monitor.
class TripEvent {
  const TripEvent({
    required this.tripEventId,
    required this.recordedByUserId,
    this.fromStatus,
    required this.toStatus,
    this.notes,
    this.snapshotLat,
    this.snapshotLng,
    required this.occurredAt,
  });

  factory TripEvent.fromJson(Map<String, dynamic> json) {
    double? parseDouble(dynamic value) {
      if (value == null) return null;
      if (value is num) return value.toDouble();
      return double.tryParse(value.toString());
    }

    DateTime parseDate(dynamic value) {
      if (value == null) return DateTime.now();
      return DateTime.tryParse(value.toString()) ?? DateTime.now();
    }

    return TripEvent(
      tripEventId: json['tripEventId'] as String? ?? '',
      recordedByUserId: json['recordedByUserId'] as String? ?? '',
      fromStatus: json['fromStatus'] as String?,
      toStatus: json['toStatus'] as String? ?? '',
      notes: json['notes'] as String?,
      snapshotLat: parseDouble(json['snapshotLat']),
      snapshotLng: parseDouble(json['snapshotLng']),
      occurredAt: parseDate(json['occurredAt']),
    );
  }

  final String tripEventId;
  final String recordedByUserId;
  final String? fromStatus;
  final String toStatus;
  final String? notes;
  final double? snapshotLat;
  final double? snapshotLng;
  final DateTime occurredAt;

  bool get hasSnapshotLocation => snapshotLat != null && snapshotLng != null;
}
