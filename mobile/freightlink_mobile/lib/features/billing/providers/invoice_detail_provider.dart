import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../data/billing_repository.dart';
import '../models/invoice.dart';

enum InvoiceDetailState { loading, loaded, error }

/// Fetches a single [Invoice] and exposes every status-change action
/// available from Invoice Detail: Agency Staff's edit/issue/confirm-payment/
/// void, and the Shipper's payment-proof submission — mirroring the Loads
/// feature's `LoadDetailProvider` shape for the equivalent Load Detail screen.
class InvoiceDetailProvider extends ChangeNotifier {
  InvoiceDetailProvider(this._repository, this.invoiceId);

  final BillingRepository _repository;
  final String invoiceId;

  InvoiceDetailState _state = InvoiceDetailState.loading;
  Invoice? _invoice;
  ApiException? _error;
  bool _isSubmittingAction = false;

  InvoiceDetailState get state => _state;
  Invoice? get invoice => _invoice;
  ApiException? get error => _error;
  bool get isSubmittingAction => _isSubmittingAction;

  Future<void> fetch() async {
    _state = InvoiceDetailState.loading;
    notifyListeners();
    try {
      _invoice = await _repository.getInvoiceById(invoiceId);
      _state = InvoiceDetailState.loaded;
    } on ApiException catch (error) {
      _error = error;
      _state = InvoiceDetailState.error;
    }
    notifyListeners();
  }

  Future<bool> issue() => _runAction(() => _repository.issueInvoice(invoiceId));

  Future<bool> confirmPayment() => _runAction(() => _repository.confirmPayment(invoiceId));

  Future<bool> voidInvoice(String reason) => _runAction(() => _repository.voidInvoice(invoiceId, reason));

  Future<bool> submitPaymentProof(String publicId) =>
      _runAction(() => _repository.submitPaymentProof(invoiceId, publicId));

  Future<bool> _runAction(Future<Invoice> Function() action) async {
    _isSubmittingAction = true;
    notifyListeners();
    try {
      _invoice = await action();
      return true;
    } on ApiException catch (error) {
      _error = error;
      return false;
    } finally {
      _isSubmittingAction = false;
      notifyListeners();
    }
  }
}
