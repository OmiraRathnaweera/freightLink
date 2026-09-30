import 'package:flutter/foundation.dart';
import '../../../core/network/api_exception.dart';
import '../data/agencies_repository.dart';

enum FleetState { loading, loaded, empty, error }

class FleetProvider extends ChangeNotifier {
  FleetProvider(this._repository);

  final AgenciesRepository _repository;

  FleetState _state = FleetState.loading;
  String? _errorMessage;
  List<Map<String, dynamic>> _vehicles = [];

  FleetState get state => _state;
  String? get errorMessage => _errorMessage;
  List<Map<String, dynamic>> get vehicles => _vehicles;

  Future<void> load() async {
    _state = FleetState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _vehicles = await _repository.getFleet();
      if (_vehicles.isEmpty) {
        _state = FleetState.empty;
      } else {
        _state = FleetState.loaded;
      }
    } on ApiException catch (e) {
      _state = FleetState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = FleetState.error;
      _errorMessage = 'Failed to load fleet. Please try again.';
    }
    notifyListeners();
  }

  Future<bool> addVehicle(Map<String, dynamic> vehicle) async {
    _state = FleetState.loading;
    notifyListeners();
    try {
      await _repository.addVehicle(vehicle);
      _vehicles.add(vehicle);
      _state = FleetState.loaded;
      notifyListeners();
      return true;
    } on ApiException catch (e) {
      _errorMessage = e.message;
      _state = _vehicles.isEmpty ? FleetState.empty : FleetState.loaded;
      notifyListeners();
      return false;
    } catch (_) {
      _errorMessage = 'Failed to add vehicle. Please try again.';
      _state = _vehicles.isEmpty ? FleetState.empty : FleetState.loaded;
      notifyListeners();
      return false;
    }
  }

  Future<bool> updateVehicleStatus(String vehicleId, String status) async {
    try {
      final updated = await _repository.updateVehicleStatus(vehicleId, status);
      final index = _vehicles.indexWhere((vehicle) => vehicle['vehicleId'] == vehicleId);
      if (index >= 0) {
        _vehicles[index] = updated;
        notifyListeners();
      }
      return true;
    } on ApiException catch (error) {
      _errorMessage = error.message;
      notifyListeners();
      return false;
    } catch (_) {
      _errorMessage = 'Failed to update vehicle status. Please try again.';
      notifyListeners();
      return false;
    }
  }
}
