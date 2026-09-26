/// Represents an Assignment / Job Proposal offered to an Agency.
enum ProposalStatus {
  proposed,
  accepted,
  declined,
  unknown;

  static ProposalStatus fromString(String? value) {
    return switch (value?.toLowerCase()) {
      'proposed' => ProposalStatus.proposed,
      'accepted' => ProposalStatus.accepted,
      'declined' => ProposalStatus.declined,
      _ => ProposalStatus.unknown,
    };
  }

  String get displayName => switch (this) {
    ProposalStatus.proposed => 'Proposed',
    ProposalStatus.accepted => 'Accepted',
    ProposalStatus.declined => 'Declined',
    ProposalStatus.unknown => 'Unknown',
  };
}

/// Detailed proposal domain model used by Agency Staff in Flutter.
class JobProposal {
  const JobProposal({
    required this.assignmentId,
    required this.loadId,
    required this.agencyId,
    this.agencyName,
    required this.proposedPrice,
    this.routedDistanceKm,
    this.proposedEtaMinutes,
    required this.status,
    required this.cargoDescription,
    this.weightKg,
    this.volumeM3,
    required this.pickupAddress,
    this.pickupLat,
    this.pickupLng,
    required this.dropoffAddress,
    this.dropoffLat,
    this.dropoffLng,
    this.pickupWindowStart,
    this.pickupWindowEnd,
    this.shipperName,
    this.referenceCode,
    this.tripId,
    this.declineReason,
    this.respondedAt,
    required this.createdAt,
    this.updatedAt,
  });

  factory JobProposal.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(dynamic value) {
      if (value == null) return null;
      return DateTime.tryParse(value.toString());
    }

    double? parseDouble(dynamic value) {
      if (value == null) return null;
      if (value is num) return value.toDouble();
      return double.tryParse(value.toString());
    }

    int? parseInt(dynamic value) {
      if (value == null) return null;
      if (value is int) return value;
      if (value is num) return value.toInt();
      return int.tryParse(value.toString());
    }

    return JobProposal(
      assignmentId: json['assignmentId'] as String? ?? '',
      loadId: json['loadId'] as String? ?? '',
      agencyId: json['agencyId'] as String? ?? '',
      agencyName: json['agencyName'] as String?,
      proposedPrice: parseDouble(json['proposedPrice']) ?? 0.0,
      routedDistanceKm: parseDouble(json['routedDistanceKm']),
      proposedEtaMinutes: parseInt(json['proposedEtaMinutes']),
      status: ProposalStatus.fromString(json['status'] as String?),
      cargoDescription: json['cargoDescription'] as String? ?? 'General Cargo',
      weightKg: parseDouble(json['weightKg']),
      volumeM3: parseDouble(json['volumeM3']),
      pickupAddress: json['pickupAddress'] as String? ?? 'Origin Address',
      pickupLat: parseDouble(json['pickupLat']),
      pickupLng: parseDouble(json['pickupLng']),
      dropoffAddress: json['dropoffAddress'] as String? ?? 'Destination Address',
      dropoffLat: parseDouble(json['dropoffLat']),
      dropoffLng: parseDouble(json['dropoffLng']),
      pickupWindowStart: parseDate(json['pickupWindowStart']),
      pickupWindowEnd: parseDate(json['pickupWindowEnd']),
      shipperName: json['shipperName'] as String?,
      referenceCode: json['referenceCode'] as String?,
      tripId: json['tripId'] as String?,
      declineReason: json['declineReason'] as String?,
      respondedAt: parseDate(json['respondedAt']),
      createdAt: parseDate(json['createdAt']) ?? DateTime.now(),
      updatedAt: parseDate(json['updatedAt']),
    );
  }

  final String assignmentId;
  final String loadId;
  final String agencyId;
  final String? agencyName;
  final double proposedPrice;
  final double? routedDistanceKm;
  final int? proposedEtaMinutes;
  final ProposalStatus status;
  final String cargoDescription;
  final double? weightKg;
  final double? volumeM3;
  final String pickupAddress;
  final double? pickupLat;
  final double? pickupLng;
  final String dropoffAddress;
  final double? dropoffLat;
  final double? dropoffLng;
  final DateTime? pickupWindowStart;
  final DateTime? pickupWindowEnd;
  final String? shipperName;
  final String? referenceCode;
  final String? tripId;
  final String? declineReason;
  final DateTime? respondedAt;
  final DateTime createdAt;
  final DateTime? updatedAt;

  bool get isProposed => status == ProposalStatus.proposed;
  bool get isAccepted => status == ProposalStatus.accepted;
  bool get isDeclined => status == ProposalStatus.declined;

  String get formattedDuration {
    if (proposedEtaMinutes == null || proposedEtaMinutes! <= 0) return 'Estimated';
    final hours = proposedEtaMinutes! ~/ 60;
    final mins = proposedEtaMinutes! % 60;
    if (hours == 0) return '${mins}m';
    if (mins == 0) return '${hours}h';
    return '${hours}h ${mins}m';
  }
}
