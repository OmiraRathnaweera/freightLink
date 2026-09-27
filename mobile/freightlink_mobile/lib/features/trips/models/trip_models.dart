import 'trip_evidence.dart';
import 'trip_event.dart';

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
    this.pickupLat,
    this.pickupLng,
    this.dropoffLat,
    this.dropoffLng,
    this.cargoDescription,
    this.weightKg,
    this.volumeM3,
    this.pickupWindowStart,
    this.pickupWindowEnd,
    this.referenceCode,
    this.routedDistanceKm,
    this.proposedEtaMinutes,
    this.events = const [],
    this.evidence = const [],
    required this.createdAt,
    this.updatedAt,
  });

  factory TripResponse.fromJson(Map<String, dynamic> json) {
    DateTime parseDate(dynamic value) {
      if (value == null) return DateTime.now();
      return DateTime.tryParse(value.toString()) ?? DateTime.now();
    }

    DateTime? tryParseDate(dynamic value) {
      if (value == null) return null;
      return DateTime.tryParse(value.toString());
    }

    double? parseDouble(dynamic value) {
      if (value == null) return null;
      if (value is num) return value.toDouble();
      return double.tryParse(value.toString());
    }

    final evidenceList = (json['evidence'] as List<dynamic>?)
            ?.map((e) => TripEvidence.fromJson(e as Map<String, dynamic>))
            .toList() ??
        const <TripEvidence>[];

    final eventsList = (json['events'] as List<dynamic>?)
            ?.map((e) => TripEvent.fromJson(e as Map<String, dynamic>))
            .toList() ??
        const <TripEvent>[];

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
      pickupLat: parseDouble(json['pickupLat']),
      pickupLng: parseDouble(json['pickupLng']),
      dropoffLat: parseDouble(json['dropoffLat']),
      dropoffLng: parseDouble(json['dropoffLng']),
      cargoDescription: json['cargoDescription'] as String?,
      weightKg: parseDouble(json['weightKg']),
      volumeM3: parseDouble(json['volumeM3']),
      pickupWindowStart: tryParseDate(json['pickupWindowStart']),
      pickupWindowEnd: tryParseDate(json['pickupWindowEnd']),
      referenceCode: json['referenceCode'] as String?,
      routedDistanceKm: parseDouble(json['routedDistanceKm']),
      proposedEtaMinutes: json['proposedEtaMinutes'] as int?,
      events: eventsList,
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
  final double? pickupLat;
  final double? pickupLng;
  final double? dropoffLat;
  final double? dropoffLng;
  final String? cargoDescription;
  final double? weightKg;
  final double? volumeM3;
  final DateTime? pickupWindowStart;
  final DateTime? pickupWindowEnd;
  final String? referenceCode;
  final double? routedDistanceKm;
  final int? proposedEtaMinutes;
  final List<TripEvent> events;
  final List<TripEvidence> evidence;
  final DateTime createdAt;
  final DateTime? updatedAt;

  bool get hasRouteCoordinates =>
      pickupLat != null && pickupLng != null && dropoffLat != null && dropoffLng != null;

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
  TripEvidence? get deliveryProof =>
      evidence.where((e) => e.isDeliveryProof).firstOrNull;

  String get formattedDistance =>
      routedDistanceKm != null ? '${routedDistanceKm!.toStringAsFixed(1)} km' : 'Distance TBD';

  String get formattedDuration {
    if (proposedEtaMinutes == null) return 'Duration TBD';
    final hours = proposedEtaMinutes! ~/ 60;
    final minutes = proposedEtaMinutes! % 60;
    if (hours > 0 && minutes > 0) return '${hours}h ${minutes}m';
    if (hours > 0) return '${hours}h transit';
    return '${minutes}m transit';
  }

  String get formattedCargoSummary {
    final parts = <String>[];
    if (weightKg != null) {
      parts.add('${weightKg!.toStringAsFixed(0)} kg');
    }
    if (volumeM3 != null) {
      parts.add('${volumeM3!.toStringAsFixed(1)} m³');
    }
    return parts.join(' • ');
  }
}
