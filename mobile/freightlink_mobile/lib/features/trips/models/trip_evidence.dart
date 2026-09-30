/// Domain models for TripEvidence, file uploads, and status changes (Component C & B).
class TripEvidence {
  const TripEvidence({
    required this.tripEvidenceId,
    required this.capturedByUserId,
    required this.evidenceType,
    required this.storageKey,
    this.secureUrl,
    this.capturedLat,
    this.capturedLng,
    required this.capturedAt,
  });

  factory TripEvidence.fromJson(Map<String, dynamic> json) {
    DateTime parseDate(dynamic value) {
      if (value == null) return DateTime.now();
      return DateTime.tryParse(value.toString()) ?? DateTime.now();
    }

    double? parseDouble(dynamic value) {
      if (value == null) return null;
      if (value is num) return value.toDouble();
      return double.tryParse(value.toString());
    }

    return TripEvidence(
      tripEvidenceId: json['tripEvidenceId'] as String? ?? '',
      capturedByUserId: json['capturedByUserId'] as String? ?? '',
      evidenceType: json['evidenceType'] as String? ?? '',
      storageKey: json['storageKey'] as String? ?? '',
      secureUrl: json['secureUrl'] as String?,
      capturedLat: parseDouble(json['capturedLat']),
      capturedLng: parseDouble(json['capturedLng']),
      capturedAt: parseDate(json['capturedAt']),
    );
  }

  final String tripEvidenceId;
  final String capturedByUserId;
  final String evidenceType;
  final String storageKey;
  final String? secureUrl;
  final double? capturedLat;
  final double? capturedLng;
  final DateTime capturedAt;

  bool get isPickupProof =>
      evidenceType.toLowerCase() == 'pickupproof' ||
      evidenceType.toLowerCase() == 'pickup_proof';

  bool get isDeliveryProof =>
      evidenceType.toLowerCase() == 'deliveryproof' ||
      evidenceType.toLowerCase() == 'delivery_proof';

  Map<String, dynamic> toJson() => {
        'tripEvidenceId': tripEvidenceId,
        'capturedByUserId': capturedByUserId,
        'evidenceType': evidenceType,
        'storageKey': storageKey,
        if (secureUrl != null) 'secureUrl': secureUrl,
        if (capturedLat != null) 'capturedLat': capturedLat,
        if (capturedLng != null) 'capturedLng': capturedLng,
        'capturedAt': capturedAt.toIso8601String(),
      };
}

/// Result returned from `POST /api/v1/files/single`.
class FileUploadResult {
  const FileUploadResult({
    required this.publicId,
    required this.secureUrl,
    this.format,
    this.bytes = 0,
    this.resourceType,
    this.contentType,
    this.originalFileName,
  });

  factory FileUploadResult.fromJson(Map<String, dynamic> json) {
    return FileUploadResult(
      publicId: json['publicId'] as String? ?? '',
      secureUrl: json['secureUrl'] as String? ?? '',
      format: json['format'] as String?,
      bytes: json['bytes'] as int? ?? 0,
      resourceType: json['resourceType'] as String?,
      contentType: json['contentType'] as String?,
      originalFileName: json['originalFileName'] as String?,
    );
  }

  final String publicId;
  final String secureUrl;
  final String? format;
  final int bytes;
  final String? resourceType;
  final String? contentType;
  final String? originalFileName;
}

/// Request model for `POST /api/v1/trips/{id}/status`.
class TripStatusChangeRequest {
  const TripStatusChangeRequest({
    required this.targetStatus,
    this.notes,
    this.snapshotLat,
    this.snapshotLng,
  });

  final String targetStatus;
  final String? notes;
  final double? snapshotLat;
  final double? snapshotLng;

  Map<String, dynamic> toJson() => {
        'targetStatus': targetStatus,
        if (notes != null && notes!.isNotEmpty) 'notes': notes,
        if (snapshotLat != null) 'snapshotLat': snapshotLat,
        if (snapshotLng != null) 'snapshotLng': snapshotLng,
      };
}
