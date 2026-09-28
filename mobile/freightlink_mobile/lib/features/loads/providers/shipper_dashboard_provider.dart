import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../billing/data/billing_repository.dart';
import '../../billing/models/invoice.dart';
import '../../billing/models/invoice_status.dart';
import '../data/loads_repository.dart';
import '../models/load_status.dart';

enum ShipperDashboardState { loading, loaded, error }

/// Drives the Shipper Dashboard tab — the Shipper counterpart to
/// [AgencyDashboardProvider], aggregating per-status load counts and invoice
/// status the same way that provider aggregates fleet/driver/compliance
/// counts: client-side, from the existing list endpoints, since there is no
/// dedicated backend dashboard-stats endpoint for either role.
class ShipperDashboardProvider extends ChangeNotifier {
  ShipperDashboardProvider(this._loadsRepository, this._billingRepository);

  final LoadsRepository _loadsRepository;
  final BillingRepository _billingRepository;

  ShipperDashboardState _state = ShipperDashboardState.loading;
  String? _errorMessage;
  Map<String, int> _loadCounts = const {};
  int _pendingInvoices = 0;

  ShipperDashboardState get state => _state;
  String? get errorMessage => _errorMessage;
  Map<String, int> get loadCounts => _loadCounts;
  int get pendingInvoices => _pendingInvoices;

  Future<void> loadStats() async {
    _state = ShipperDashboardState.loading;
    _errorMessage = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        _loadsRepository.getList(pageSize: 1),
        _loadsRepository.getList(pageSize: 1, status: LoadStatus.posted),
        _loadsRepository.getList(pageSize: 1, status: LoadStatus.matched),
        _loadsRepository.getList(pageSize: 1, status: LoadStatus.inTransit),
        _loadsRepository.getList(pageSize: 1, status: LoadStatus.delivered),
      ]);

      _loadCounts = {
        'total': results[0].totalItems,
        'posted': results[1].totalItems,
        'matched': results[2].totalItems,
        'inTransit': results[3].totalItems,
        'delivered': results[4].totalItems,
      };

      final invoices = await _billingRepository.getInvoices().catchError(
            (_) => <Invoice>[],
          );
      _pendingInvoices = invoices.where((i) => i.status == InvoiceStatus.issued).length;

      _state = ShipperDashboardState.loaded;
    } on ApiException catch (e) {
      _state = ShipperDashboardState.error;
      _errorMessage = e.message;
    } catch (_) {
      _state = ShipperDashboardState.error;
      _errorMessage = 'Failed to load dashboard. Please try again.';
    }
    notifyListeners();
  }
}
