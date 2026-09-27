import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/billing_repository.dart';
import '../models/invoice.dart';
import '../models/invoice_status.dart';

enum InvoiceListState { loading, loaded, empty, error }

/// Drives the invoice list shown on the Payments tab — the backend already
/// scopes results by role (`InvoicesController.GetList`: a Shipper sees only
/// their own non-Draft invoices, Agency Staff sees invoices tied to their
/// agency's trips), so this one provider backs both the Shipper and Agency
/// Staff views of the same screen.
class InvoiceListProvider extends ChangeNotifier {
  InvoiceListProvider(this._repository);

  final BillingRepository _repository;

  InvoiceListState _state = InvoiceListState.loading;
  List<Invoice> _items = [];
  InvoiceStatus? _statusFilter;
  ApiException? _error;

  InvoiceListState get state => _state;
  List<Invoice> get items => _statusFilter == null
      ? _items
      : _items.where((invoice) => invoice.status == _statusFilter).toList();
  InvoiceStatus? get statusFilter => _statusFilter;
  ApiException? get error => _error;

  Future<void> load() async {
    _state = InvoiceListState.loading;
    notifyListeners();

    try {
      _items = await _repository.getInvoices();
      _state = _items.isEmpty ? InvoiceListState.empty : InvoiceListState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = InvoiceListState.error;
    }
    notifyListeners();
  }

  void setStatusFilter(InvoiceStatus? status) {
    if (_statusFilter == status) return;
    _statusFilter = status;
    notifyListeners();
  }
}
