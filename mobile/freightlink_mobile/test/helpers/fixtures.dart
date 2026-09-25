import 'package:freightlink_mobile/core/models/paged_result.dart';
import 'package:freightlink_mobile/features/loads/models/load.dart';
import 'package:freightlink_mobile/features/loads/models/load_status.dart';

/// Test-only fixtures for the shipper Loads models. Built directly via the
/// model constructors (no JSON round-trip needed) with sane defaults, so
/// each test only names the fields it actually cares about.
LoadListItem buildLoadListItem({
  String loadId = 'load-1',
  String shipperUserId = 'shipper-1',
  String shipperName = 'Acme Traders',
  String referenceCode = 'FM-7942',
  String cargoDescription = 'Palletized dry goods',
  double weightKg = 1200,
  String pickupAddress = 'Colombo Port, Colombo',
  String dropoffAddress = 'Kandy Yard, Kandy',
  DateTime? pickupWindowStart,
  DateTime? pickupWindowEnd,
  double? estimatedPrice = 42500,
  LoadStatus status = LoadStatus.posted,
  DateTime? createdAt,
}) {
  final now = DateTime(2026, 1, 1, 9);
  return LoadListItem(
    loadId: loadId,
    shipperUserId: shipperUserId,
    shipperName: shipperName,
    referenceCode: referenceCode,
    cargoDescription: cargoDescription,
    weightKg: weightKg,
    pickupAddress: pickupAddress,
    dropoffAddress: dropoffAddress,
    pickupWindowStart: pickupWindowStart ?? now,
    pickupWindowEnd: pickupWindowEnd ?? now.add(const Duration(hours: 4)),
    estimatedPrice: estimatedPrice,
    status: status,
    createdAt: createdAt ?? now,
  );
}

Load buildLoad({
  String loadId = 'load-1',
  String shipperUserId = 'shipper-1',
  String shipperName = 'Acme Traders',
  String referenceCode = 'FM-7942',
  String cargoDescription = 'Palletized dry goods',
  double weightKg = 1200,
  double volumeM3 = 8,
  String pickupAddress = 'Colombo Port, Colombo',
  double pickupLat = 6.9344,
  double pickupLng = 79.8428,
  String dropoffAddress = 'Kandy Yard, Kandy',
  double dropoffLat = 7.2906,
  double dropoffLng = 80.6337,
  DateTime? pickupWindowStart,
  DateTime? pickupWindowEnd,
  double? estimatedPrice = 42500,
  LoadStatus status = LoadStatus.posted,
  DateTime? createdAt,
  DateTime? updatedAt,
  List<LoadStatusEvent> statusHistory = const [],
}) {
  final now = DateTime(2026, 1, 1, 9);
  return Load(
    loadId: loadId,
    shipperUserId: shipperUserId,
    shipperName: shipperName,
    referenceCode: referenceCode,
    cargoDescription: cargoDescription,
    weightKg: weightKg,
    volumeM3: volumeM3,
    pickupAddress: pickupAddress,
    pickupLat: pickupLat,
    pickupLng: pickupLng,
    dropoffAddress: dropoffAddress,
    dropoffLat: dropoffLat,
    dropoffLng: dropoffLng,
    pickupWindowStart: pickupWindowStart ?? now,
    pickupWindowEnd: pickupWindowEnd ?? now.add(const Duration(hours: 4)),
    estimatedPrice: estimatedPrice,
    status: status,
    createdAt: createdAt ?? now,
    updatedAt: updatedAt ?? now,
    statusHistory: statusHistory,
  );
}

PagedResult<LoadListItem> buildPagedResult(List<LoadListItem> items) {
  return PagedResult<LoadListItem>(
    items: items,
    page: 1,
    pageSize: 20,
    totalItems: items.length,
    totalPages: 1,
  );
}
