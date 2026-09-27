import 'package:flutter/foundation.dart';
import '../../../core/network/api_exception.dart';
import '../data/agencies_repository.dart';

class DriverOnboardingProvider extends ChangeNotifier {
  DriverOnboardingProvider(this._repository);

  final AgenciesRepository _repository;

  bool _isSubmitting = false;
  String? _errorMessage;
  bool _isSuccess = false;

  bool get isSubmitting => _isSubmitting;
  String? get errorMessage => _errorMessage;
  bool get isSuccess => _isSuccess;

  Future<void> submitDriver(Map<String, dynamic> driverData) async {
    _isSubmitting = true;
    _errorMessage = null;
    _isSuccess = false;
    notifyListeners();

    try {
      await _repository.addDriver(driverData);
      _isSuccess = true;
    } on ApiException catch (e) {
      _errorMessage = e.message;
      _isSuccess = false;
    } catch (_) {
      _errorMessage = 'Failed to register driver. Please try again.';
      _isSuccess = false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }
}
