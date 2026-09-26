import 'package:flutter/foundation.dart';
import '../data/agencies_repository.dart';

enum DashboardState { loading, loaded, error }

class AgencyDashboardProvider extends ChangeNotifier {
  AgencyDashboardProvider(this._repository);

  final AgenciesRepository _repository;

  DashboardState _state = DashboardState.loading;
  String? _errorMessage;
  Map<String, dynamic>? _stats;

  DashboardState get state => _state;
  String? get errorMessage => _errorMessage;
  Map<String, dynamic>? get stats => _stats;

  Future<void> loadStats() async {
    _state = DashboardState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _stats = await _repository.getDashboardStats();
      _state = DashboardState.loaded;
    } catch (e) {
      _state = DashboardState.error;
      _errorMessage = e.toString();
    }
    notifyListeners();
  }
}
