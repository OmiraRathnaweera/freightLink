import 'package:flutter/foundation.dart';
import '../data/agencies_repository.dart';

enum DashboardState { loading, loaded, error }

class AgencyDashboardProvider extends ChangeNotifier {
  AgencyDashboardProvider(this._repository);

  final AgenciesRepository _repository;

  DashboardState _state = DashboardState.loading;
  String? _errorMessage;
  Map<String, dynamic>? _stats;
  Map<String, dynamic>? _agencyProfile;

  DashboardState get state => _state;
  String? get errorMessage => _errorMessage;
  Map<String, dynamic>? get stats => _stats;
  Map<String, dynamic>? get agencyProfile => _agencyProfile;

  Future<void> loadStats() async {
    _state = DashboardState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _repository.getDashboardStats(),
        _repository.getProfile().catchError((_) => <String, dynamic>{}),
      ]);
      _stats = results[0];
      final profile = results[1];
      if (profile.isNotEmpty) {
        _agencyProfile = profile;
      }
      _state = DashboardState.loaded;
    } catch (e) {
      _state = DashboardState.error;
      _errorMessage = e.toString();
    }
    notifyListeners();
  }
}
