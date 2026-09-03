import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/loads_repository.dart';
import '../models/load.dart';
import '../models/load_status.dart';

enum LoadsListState { loading, loaded, empty, error }

/// Drives any paginated Loads list screen. The backend already scopes
/// results by role (`LoadsController.GetList`: a Shipper only ever sees
/// their own loads, an Admin sees every load) — so this one class backs both
/// **My Loads** and the Admin **All Loads** screen; each screen creates its
/// own instance.
class LoadsListProvider extends ChangeNotifier {
  LoadsListProvider(this._repository);

  final LoadsRepository _repository;

  LoadsListState _state = LoadsListState.loading;
  List<LoadListItem> _items = [];
  String _search = '';
  LoadStatus? _statusFilter;
  ApiException? _error;
  Timer? _debounce;

  LoadsListState get state => _state;
  List<LoadListItem> get items => _items;
  String get search => _search;
  LoadStatus? get statusFilter => _statusFilter;
  ApiException? get error => _error;

  Future<void> load() async {
    _state = LoadsListState.loading;
    notifyListeners();

    try {
      final page = await _repository.getList(
        search: _search,
        status: _statusFilter,
      );
      _items = page.items;
      _state = _items.isEmpty ? LoadsListState.empty : LoadsListState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = LoadsListState.error;
    }
    notifyListeners();
  }

  /// Debounced so typing in the search field doesn't fire a request per
  /// keystroke.
  void onSearchChanged(String value) {
    _search = value;
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), load);
  }

  void setStatusFilter(LoadStatus? status) {
    if (_statusFilter == status) return;
    _statusFilter = status;
    load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }
}
