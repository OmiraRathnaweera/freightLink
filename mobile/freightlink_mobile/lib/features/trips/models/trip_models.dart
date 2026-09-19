import 'trip_evidence.dart';

/// Request payload for dispatching and creating a new Trip (POST /api/v1/trips).
class CreateTripRequest {
  const CreateTripRequest({
    required this.assignmentId,
    required this.vehicleId,
    required this.driverId,
    this.notes,
  });

  final String assignmentId;
  final String vehicleId;
  final String driverId;
  final String? notes;

  Map<String, dynamic> toJson() => {
    'assignmentId': assignmentId,
    'vehicleId': vehicleId,
    'driverId': driverId,
    if (notes != null && notes!.isNotEmpty) 'notes': notes,
  };
}

/// Response returned when a trip is successfully dispatched / created.
class TripResponse {
  const TripResponse({
    required this.tripId,
    required this.assignmentId,
    required this.loadId,
    required this.agencyId,
    this.agencyName,
    required this.vehicleId,
    this.vehicleRegistrationNo,
    required this.driverId,
    this.driverName,
    required this.status,
    this.pickupAddress,
    this.dropoffAddress,
    this.evidence = const [],
    required this.createdAt,
    this.updatedAt,
  });

  factory TripResponse.fromJson(Map<String, dynamic> json) {
    DateTime parseDate(dynamic value) {
      if (value == null) return DateTime.now();
      return DateTime.tryParse(value.toString()) ?? DateTime.now();
    }

    final evidenceList = (json['evidence'] as List<dynamic>?)
            ?.map((e) => TripEvidence.fromJson(e as Map<String, dynamic>))
            .toList() ??
        const <TripEvidence>[];

    return TripResponse(
      tripId: json['tripId'] as String? ?? '',
      assignmentId: json['assignmentId'] as String? ?? '',
      loadId: json['loadId'] as String? ?? '',
      agencyId: json['agencyId'] as String? ?? '',
      agencyName: json['agencyName'] as String?,
      vehicleId: json['vehicleId'] as String? ?? '',
      vehicleRegistrationNo: json['vehicleRegistrationNo'] as String?,
      driverId: json['driverId'] as String? ?? '',
      driverName: json['driverName'] as String?,
      status: json['status'] as String? ?? 'Assigned',
      pickupAddress: json['pickupAddress'] as String?,
      dropoffAddress: json['dropoffAddress'] as String?,
      evidence: evidenceList,
      createdAt: parseDate(json['createdAt']),
      updatedAt: json['updatedAt'] != null ? parseDate(json['updatedAt']) : null,
    );
  }

  final String tripId;
  final String assignmentId;
  final String loadId;
  final String agencyId;
  final String? agencyName;
  final String vehicleId;
  final String? vehicleRegistrationNo;
  final String driverId;
  final String? driverName;
  final String status;
  final String? pickupAddress;
  final String? dropoffAddress;
  final List<TripEvidence> evidence;
  final DateTime createdAt;
  final DateTime? updatedAt;

  bool get isAssigned => status.toLowerCase() == 'assigned';
  bool get isPickedUp =>
      status.toLowerCase() == 'pickedup' || status.toLowerCase() == 'picked_up';
  bool get isInTransit =>
      status.toLowerCase() == 'intransit' || status.toLowerCase() == 'in_transit';
  bool get isDelivered => status.toLowerCase() == 'delivered';

  bool get hasPickupProof => evidence.any((e) => e.isPickupProof);
  bool get hasDeliveryProof => evidence.any((e) => e.isDeliveryProof);
  TripEvidence? get pickupProof =>
      evidence.where((e) => e.isPickupProof).firstOrNull;
}
