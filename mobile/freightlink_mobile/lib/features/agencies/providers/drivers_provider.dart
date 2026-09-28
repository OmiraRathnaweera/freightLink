import 'package:flutter/foundation.dart';
import '../../../core/network/api_exception.dart';
import '../data/agencies_repository.dart';

enum DriversState { loading, loaded, empty, error }

class DriversProvider extends ChangeNotifier {
  DriversProvider(this._repository);

  final AgenciesRepository _repository;

  DriversState _state = DriversState.loading;
  String? _errorMessage;
  List<Map<String, dynamic>> _drivers = [];

  DriversState get state => _state;
  String? get errorMessage => _errorMessage;
  List<Map<String, dynamic>> get drivers => _drivers;

  Future<void> load() async {
    _state = DriversState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _drivers = await _repository.getDrivers();
      if (_drivers.isEmpty) {
        _state = DriversState.empty;
      } else {
        _state = DriversState.loaded;
      }
    } on ApiException catch (e) {
      _state = DriversState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = DriversState.error;
      _errorMessage = 'Failed to load drivers. Please try again.';
    }
    notifyListeners();
  }
}
