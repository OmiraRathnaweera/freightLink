import 'load_status.dart';

/// One recorded status transition, mirroring `LoadStatusHistoryResponseDto`.
/// Drives `LoadTimeline` on the Load Detail screen.
class LoadStatusEvent {
  const LoadStatusEvent({
    required this.id,
    this.fromStatus,
    required this.toStatus,
    this.reason,
    required this.changedAt,
  });

  factory LoadStatusEvent.fromJson(Map<String, dynamic> json) {
    return LoadStatusEvent(
      id: json['loadStatusHistoryId'] as String,
      fromStatus: json['fromStatus'] as String?,
      toStatus: json['toStatus'] as String,
      reason: json['reason'] as String?,
      changedAt: DateTime.parse(json['changedAt'] as String),
    );
  }

  final String id;
  final String? fromStatus;
  final String toStatus;
  final String? reason;
  final DateTime changedAt;
}

/// The lightweight row shape for `GET /api/v1/loads` list results, mirroring
/// `LoadListItemDto`. Used by My Loads and the Admin All Loads list.
class LoadListItem {
  const LoadListItem({
    required this.loadId,
    required this.shipperUserId,
    required this.shipperName,
    required this.referenceCode,
    required this.cargoDescription,
    required this.weightKg,
    required this.pickupAddress,
    required this.dropoffAddress,
    required this.pickupWindowStart,
    required this.pickupWindowEnd,
    this.estimatedPrice,
    required this.status,
    required this.createdAt,
  });

  factory LoadListItem.fromJson(Map<String, dynamic> json) {
    return LoadListItem(
      loadId: json['loadId'] as String,
      shipperUserId: json['shipperUserId'] as String,
      shipperName: json['shipperName'] as String? ?? 'Unknown',
      referenceCode: json['referenceCode'] as String,
      cargoDescription: json['cargoDescription'] as String,
      weightKg: (json['weightKg'] as num).toDouble(),
      pickupAddress: json['pickupAddress'] as String,
      dropoffAddress: json['dropoffAddress'] as String,
      pickupWindowStart: DateTime.parse(json['pickupWindowStart'] as String),
      pickupWindowEnd: DateTime.parse(json['pickupWindowEnd'] as String),
      estimatedPrice: (json['estimatedPrice'] as num?)?.toDouble(),
      status: LoadStatus.fromWire(json['status'] as String),
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  final String loadId;
  final String shipperUserId;
  final String shipperName;
  final String referenceCode;
  final String cargoDescription;
  final double weightKg;
  final String pickupAddress;
  final String dropoffAddress;
  final DateTime pickupWindowStart;
  final DateTime pickupWindowEnd;
  final double? estimatedPrice;
  final LoadStatus status;
  final DateTime createdAt;
}

/// The full single-load shape, mirroring `LoadResponseDto`. Used by Load
/// Detail, and as the source of truth pre-filling Edit Load.
class Load {
  const Load({
    required this.loadId,
    required this.shipperUserId,
    required this.shipperName,
    required this.referenceCode,
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
    this.estimatedPrice,
    required this.status,
    this.workflowRunId,
    this.tripId,
    required this.createdAt,
    required this.updatedAt,
    this.statusHistory = const [],
  });

  factory Load.fromJson(Map<String, dynamic> json) {
    return Load(
      loadId: json['loadId'] as String,
      shipperUserId: json['shipperUserId'] as String,
      shipperName: json['shipperName'] as String? ?? 'Unknown',
      referenceCode: json['referenceCode'] as String,
      cargoDescription: json['cargoDescription'] as String,
      weightKg: (json['weightKg'] as num).toDouble(),
      volumeM3: (json['volumeM3'] as num).toDouble(),
      pickupAddress: json['pickupAddress'] as String,
      pickupLat: (json['pickupLat'] as num).toDouble(),
      pickupLng: (json['pickupLng'] as num).toDouble(),
      dropoffAddress: json['dropoffAddress'] as String,
      dropoffLat: (json['dropoffLat'] as num).toDouble(),
      dropoffLng: (json['dropoffLng'] as num).toDouble(),
      pickupWindowStart: DateTime.parse(json['pickupWindowStart'] as String),
      pickupWindowEnd: DateTime.parse(json['pickupWindowEnd'] as String),
      estimatedPrice: (json['estimatedPrice'] as num?)?.toDouble(),
      status: LoadStatus.fromWire(json['status'] as String),
      workflowRunId: json['workflowRunId'] as String?,
      tripId: json['tripId'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
      statusHistory: (json['statusHistory'] as List<dynamic>? ?? [])
          .cast<Map<String, dynamic>>()
          .map(LoadStatusEvent.fromJson)
          .toList(),
    );
  }

  final String loadId;
  final String shipperUserId;
  final String shipperName;
  final String referenceCode;
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
  final double? estimatedPrice;
  final LoadStatus status;
  final String? workflowRunId;
  final String? tripId;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<LoadStatusEvent> statusHistory;
}
