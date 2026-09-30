import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/loads_repository.dart';
import '../models/load.dart';

enum LoadDetailState { loading, loaded, error }

/// Fetches a single [Load] (with its `StatusHistory`) and exposes the
/// status-change actions available from Load Detail: publishing a Draft, and
/// cancelling.
class LoadDetailProvider extends ChangeNotifier {
  LoadDetailProvider(this._repository, this.loadId);

  final LoadsRepository _repository;
  final String loadId;

  LoadDetailState _state = LoadDetailState.loading;
  Load? _load;
  ApiException? _error;
  bool _isUpdatingStatus = false;

  LoadDetailState get state => _state;
  Load? get load => _load;
  ApiException? get error => _error;
  bool get isUpdatingStatus => _isUpdatingStatus;

  Future<void> fetch() async {
    _state = LoadDetailState.loading;
    notifyListeners();
    try {
      _load = await _repository.getById(loadId);
      _state = LoadDetailState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = LoadDetailState.error;
    }
    notifyListeners();
  }

  Future<bool> postLoad() => _changeStatus(() => _repository.post(loadId));

  Future<bool> cancelLoad(String reason) =>
      _changeStatus(() => _repository.cancel(loadId, reason: reason));

  Future<bool> _changeStatus(Future<Load> Function() action) async {
    _isUpdatingStatus = true;
    notifyListeners();
    try {
      _load = await action();
      return true;
    } on ApiException catch (error) {
      _error = error;
      return false;
    } finally {
      _isUpdatingStatus = false;
      notifyListeners();
    }
  }
}
