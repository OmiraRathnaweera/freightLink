import 'package:flutter/foundation.dart';
import '../../../core/network/api_exception.dart';
import '../data/agencies_repository.dart';

enum ProfileState { loading, loaded, saving, error }

class AgencyProfileProvider extends ChangeNotifier {
  AgencyProfileProvider(this._repository);

  final AgenciesRepository _repository;
  ProfileState _state = ProfileState.loading;
  String? _errorMessage;
  Map<String, dynamic>? _profileData;

  ProfileState get state => _state;
  String? get errorMessage => _errorMessage;
  Map<String, dynamic>? get profileData => _profileData;

  Future<void> loadProfile() async {
    _state = ProfileState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      _profileData = await _repository.getProfile();
      _state = ProfileState.loaded;
    } on ApiException catch (e) {
      _state = ProfileState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = ProfileState.error;
      _errorMessage = 'Failed to load agency profile. Please try again.';
    }
    notifyListeners();
  }

  Future<bool> updateProfile(Map<String, dynamic> newData) async {
    _state = ProfileState.saving;
    _errorMessage = null;
    notifyListeners();

    try {
      await _repository.updateProfile(newData);
      _profileData = {
        ...?_profileData,
        ...newData,
      };
      _state = ProfileState.loaded;
      notifyListeners();
      return true;
    } on ApiException catch (e) {
      _state = ProfileState.loaded; // Revert to loaded, just show error
      _errorMessage = e.message;
      notifyListeners();
      return false;
    } catch (_) {
      _state = ProfileState.loaded;
      _errorMessage = 'Failed to update agency profile. Please try again.';
      notifyListeners();
      return false;
    }
  }
}
