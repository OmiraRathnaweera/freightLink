import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../notifications/models/in_app_notification.dart';
import '../../notifications/providers/notification_provider.dart';
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
  LoadsListProvider(this._repository, [this._notificationProvider]);

  final LoadsRepository _repository;
  final NotificationProvider? _notificationProvider;
  final Map<String, LoadStatus> _knownStatuses = {};

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

      for (final item in page.items) {
        final previousStatus = _knownStatuses[item.loadId];
        if (previousStatus != null &&
            previousStatus != item.status &&
            item.status == LoadStatus.matched) {
          _notificationProvider?.pushNotification(
            title: 'Load Matched!',
            message: 'Your load ${item.referenceCode} has been matched and approved by dispatch.',
            category: NotificationCategory.loadMatched,
            referenceId: item.loadId,
          );
        }
        _knownStatuses[item.loadId] = item.status;
      }

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
