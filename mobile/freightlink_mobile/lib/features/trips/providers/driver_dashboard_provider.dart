import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/trips_repository.dart';

enum DriverDashboardState { loading, loaded, error }

/// Drives the Driver Dashboard tab — the Driver counterpart to
/// [ShipperDashboardProvider]/`AgencyDashboardProvider`, aggregating
/// per-status trip counts (most notably completed rides) client-side from
/// the existing `GET /trips` list endpoint, which the backend auto-scopes to
/// the calling Driver's own trips.
class DriverDashboardProvider extends ChangeNotifier {
  DriverDashboardProvider(this._repository);

  final TripsRepository _repository;

  DriverDashboardState _state = DriverDashboardState.loading;
  String? _errorMessage;
  Map<String, int> _tripCounts = const {};

  DriverDashboardState get state => _state;
  String? get errorMessage => _errorMessage;
  Map<String, int> get tripCounts => _tripCounts;

  Future<void> loadStats() async {
    _state = DriverDashboardState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getTrips(pageSize: 1),
        _repository.getTrips(pageSize: 1, status: 'Assigned'),
        _repository.getTrips(pageSize: 1, status: 'PickedUp'),
        _repository.getTrips(pageSize: 1, status: 'InTransit'),
        _repository.getTrips(pageSize: 1, status: 'Delivered'),
        _repository.getTrips(pageSize: 1, status: 'Cancelled'),
      ]);

      _tripCounts = {
        'total': results[0].totalItems,
        'assigned': results[1].totalItems,
        'pickedUp': results[2].totalItems,
        'inTransit': results[3].totalItems,
        'delivered': results[4].totalItems,
        'cancelled': results[5].totalItems,
      };

      _state = DriverDashboardState.loaded;
    } on ApiException catch (e) {
      _state = DriverDashboardState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = DriverDashboardState.error;
      _errorMessage = 'Failed to load dashboard. Please try again.';
    }
    notifyListeners();
  }
}
