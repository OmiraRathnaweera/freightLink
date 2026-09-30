import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

/// Backend `DisputeCategory` values. Keep these wire names synchronized with
/// `backend/Entities/Enums/DisputeCategory.cs`.
enum DisputeCategory {
  damage,
  delay,
  billing,
  other;

  static DisputeCategory fromWire(String value) =>
      DisputeCategory.values.firstWhere(
        (category) => category.wireName.toLowerCase() == value.toLowerCase(),
        orElse: () => throw ArgumentError('Unknown DisputeCategory: $value'),
      );

  String get wireName => switch (this) {
    DisputeCategory.damage => 'Damage',
    DisputeCategory.delay => 'Delay',
    DisputeCategory.billing => 'Billing',
    DisputeCategory.other => 'Other',
  };

  String get label => wireName;
}

/// Backend `DisputeStatus` values. The app deliberately exposes only the
/// legal lifecycle, Raised -> UnderReview -> Resolved.
enum DisputeStatus {
  raised,
  underReview,
  resolved;

  static DisputeStatus fromWire(String value) =>
      DisputeStatus.values.firstWhere(
        (status) => status.wireName.toLowerCase() == value.toLowerCase(),
        orElse: () => throw ArgumentError('Unknown DisputeStatus: $value'),
      );

  String get wireName => switch (this) {
    DisputeStatus.raised => 'Raised',
    DisputeStatus.underReview => 'UnderReview',
    DisputeStatus.resolved => 'Resolved',
  };

  String get label => switch (this) {
    DisputeStatus.raised => 'Raised',
    DisputeStatus.underReview => 'Under Review',
    DisputeStatus.resolved => 'Resolved',
  };

  Color get foreground => switch (this) {
    DisputeStatus.raised => AppColors.statusMatchedFg,
    DisputeStatus.underReview => AppColors.statusInTransitFg,
    DisputeStatus.resolved => AppColors.statusSuccessFg,
  };

  Color get background => switch (this) {
    DisputeStatus.raised => AppColors.statusMatchedBg,
    DisputeStatus.underReview => AppColors.statusInTransitBg,
    DisputeStatus.resolved => AppColors.statusSuccessBg,
  };
}

enum DisputeOutcome {
  upheld,
  partiallyUpheld,
  rejected;

  static DisputeOutcome fromWire(String value) =>
      DisputeOutcome.values.firstWhere(
        (outcome) => outcome.wireName.toLowerCase() == value.toLowerCase(),
        orElse: () => throw ArgumentError('Unknown DisputeOutcome: $value'),
      );

  String get wireName => switch (this) {
    DisputeOutcome.upheld => 'Upheld',
    DisputeOutcome.partiallyUpheld => 'PartiallyUpheld',
    DisputeOutcome.rejected => 'Rejected',
  };

  String get label => switch (this) {
    DisputeOutcome.upheld => 'Upheld',
    DisputeOutcome.partiallyUpheld => 'Partially Upheld',
    DisputeOutcome.rejected => 'Rejected',
  };
}

class DisputeResolution {
  const DisputeResolution({
    required this.disputeId,
    required this.resolvedByUserId,
    required this.outcome,
    required this.notes,
    required this.resolvedAt,
  });

  factory DisputeResolution.fromJson(Map<String, dynamic> json) =>
      DisputeResolution(
        disputeId: json['disputeId'] as String,
        resolvedByUserId: json['resolvedByUserId'] as String,
        outcome: DisputeOutcome.fromWire(json['outcome'] as String),
        notes: json['notes'] as String? ?? '',
        resolvedAt: DateTime.parse(json['resolvedAt'] as String),
      );

  final String disputeId;
  final String resolvedByUserId;
  final DisputeOutcome outcome;
  final String notes;
  final DateTime resolvedAt;
}

/// Typed representation shared by the backend list and detail endpoints.
/// List-only enrichment is nullable because the single-resource endpoint
/// deliberately remains backwards-compatible with the original DTO shape.
class Dispute {
  const Dispute({
    required this.disputeId,
    required this.tripId,
    required this.raisedByUserId,
    required this.category,
    required this.description,
    required this.status,
    required this.createdAt,
    this.updatedAt,
    this.raisedByName,
    this.raisedByRole,
    this.tripRouteSummary,
    this.carrierAgencyName,
    this.vehicleRegistrationNo,
    this.resolution,
  });

  factory Dispute.fromJson(Map<String, dynamic> json) => Dispute(
    disputeId: json['disputeId'] as String,
    tripId: json['tripId'] as String,
    raisedByUserId: json['raisedByUserId'] as String,
    category: DisputeCategory.fromWire(json['category'] as String),
    description: json['description'] as String? ?? '',
    status: DisputeStatus.fromWire(json['status'] as String),
    createdAt: DateTime.parse(json['createdAt'] as String),
    updatedAt: json['updatedAt'] == null
        ? null
        : DateTime.parse(json['updatedAt'] as String),
    raisedByName: json['raisedByName'] as String?,
    raisedByRole: json['raisedByRole'] as String?,
    tripRouteSummary: json['tripRouteSummary'] as String?,
    carrierAgencyName: json['carrierAgencyName'] as String?,
    vehicleRegistrationNo: json['vehicleRegistrationNo'] as String?,
    resolution: json['resolution'] == null
        ? null
        : DisputeResolution.fromJson(
            json['resolution'] as Map<String, dynamic>,
          ),
  );

  final String disputeId;
  final String tripId;
  final String raisedByUserId;
  final DisputeCategory category;
  final String description;
  final DisputeStatus status;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final String? raisedByName;
  final String? raisedByRole;
  final String? tripRouteSummary;
  final String? carrierAgencyName;
  final String? vehicleRegistrationNo;
  final DisputeResolution? resolution;
}
