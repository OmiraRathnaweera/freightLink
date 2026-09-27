import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/dispute_repository.dart';
import '../models/dispute.dart';

enum DisputeListState { loading, loaded, empty, error }

enum DisputeDetailState { loading, loaded, error }

class DisputeListProvider extends ChangeNotifier {
  DisputeListProvider(this._repository);

  final DisputeRepository _repository;
  DisputeListState _state = DisputeListState.loading;
  List<Dispute> _items = const [];
  DisputeStatus? _statusFilter;
  ApiException? _error;

  DisputeListState get state => _state;
  List<Dispute> get items => _items;
  DisputeStatus? get statusFilter => _statusFilter;
  ApiException? get error => _error;

  Future<void> load() async {
    _state = DisputeListState.loading;
    _error = null;
    notifyListeners();
    try {
      _items = await _repository.getMyDisputes(status: _statusFilter);
      _state = _items.isEmpty
          ? DisputeListState.empty
          : DisputeListState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = DisputeListState.error;
    }
    notifyListeners();
  }

  Future<void> setStatusFilter(DisputeStatus? value) async {
    if (_statusFilter == value) return;
    _statusFilter = value;
    await load();
  }
}

class DisputeDetailProvider extends ChangeNotifier {
  DisputeDetailProvider(this._repository, this.disputeId);

  final DisputeRepository _repository;
  final String disputeId;
  DisputeDetailState _state = DisputeDetailState.loading;
  Dispute? _dispute;
  ApiException? _error;

  DisputeDetailState get state => _state;
  Dispute? get dispute => _dispute;
  ApiException? get error => _error;

  Future<void> fetch() async {
    _state = DisputeDetailState.loading;
    _error = null;
    notifyListeners();
    try {
      _dispute = await _repository.getDisputeDetail(disputeId);
      _state = DisputeDetailState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = DisputeDetailState.error;
    }
    notifyListeners();
  }
}
