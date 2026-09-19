/// Represents an agency vehicle available for assignment.
class FleetVehicle {
  const FleetVehicle({
    required this.vehicleId,
    required this.agencyId,
    required this.registrationNo,
    required this.vehicleType,
    required this.capacityKg,
    required this.volumeM3,
    required this.status,
    required this.isAvailable,
  });

  factory FleetVehicle.fromJson(Map<String, dynamic> json) {
    double parseDouble(dynamic value) {
      if (value == null) return 0.0;
      if (value is num) return value.toDouble();
      return double.tryParse(value.toString()) ?? 0.0;
    }

    final statusStr = json['status'] as String? ?? '';
    final isAvail = json['isAvailable'] as bool? ?? (statusStr.toLowerCase() == 'available');

    return FleetVehicle(
      vehicleId: json['vehicleId'] as String? ?? '',
      agencyId: json['agencyId'] as String? ?? '',
      registrationNo: json['registrationNo'] as String? ?? '',
      vehicleType: json['vehicleType'] as String? ?? 'Vehicle',
      capacityKg: parseDouble(json['capacityKg']),
      volumeM3: parseDouble(json['volumeM3']),
      status: statusStr,
      isAvailable: isAvail,
    );
  }

  final String vehicleId;
  final String agencyId;
  final String registrationNo;
  final String vehicleType;
  final double capacityKg;
  final double volumeM3;
  final String status;
  final bool isAvailable;

  String get summary => '$registrationNo ($vehicleType, ${capacityKg.toInt()} kg)';
}

/// Represents an agency driver available for assignment.
class FleetDriver {
  const FleetDriver({
    required this.driverId,
    required this.userId,
    required this.agencyId,
    required this.fullName,
    this.email,
    required this.licenceNo,
    required this.status,
    required this.isActive,
  });

  factory FleetDriver.fromJson(Map<String, dynamic> json) {
    final statusStr = json['status'] as String? ?? '';
    final isAct = json['isActive'] as bool? ?? (statusStr.toLowerCase() == 'active');

    return FleetDriver(
      driverId: json['driverId'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
      agencyId: json['agencyId'] as String? ?? '',
      fullName: json['fullName'] as String? ?? 'Driver',
      email: json['email'] as String?,
      licenceNo: json['licenceNo'] as String? ?? '',
      status: statusStr,
      isActive: isAct,
    );
  }

  final String driverId;
  final String userId;
  final String agencyId;
  final String fullName;
  final String? email;
  final String licenceNo;
  final String status;
  final bool isActive;

  String get summary => '$fullName (Licence: $licenceNo)';
}
