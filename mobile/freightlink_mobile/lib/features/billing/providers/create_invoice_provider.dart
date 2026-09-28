import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../trips/data/trips_repository.dart';
import '../../trips/models/trip_models.dart';
import '../data/billing_repository.dart';
import '../models/invoice.dart';

enum CreateInvoiceTripState { loading, loaded, error }

/// Drives the Create/Edit Invoice form (Agency Staff only) — shared by both creating a new invoice
/// (entered from a `Delivered` trip's detail screen) and editing an existing Draft, exactly like the
/// web app's single `InvoiceFormModal` serves both cases. This system has no direct customers and no
/// manual price negotiation at invoicing time: every invoice bills the trip's shipper at a single
/// amount, defaulted from the accepted assignment's already-agreed price
/// ([TripResponse.agreedPrice]) — there is no line-item list, currency choice, discount, or due date.
class CreateInvoiceProvider extends ChangeNotifier {
  CreateInvoiceProvider(this._repository, this._tripsRepository, {this.tripId, this.editingInvoice}) {
    final editing = editingInvoice;
    if (editing != null) {
      amountText = _trimZeros(editing.totalAmount);
      notes = editing.notes;
    } else if (tripId != null) {
      _loadTrip();
    }
  }

  final BillingRepository _repository;
  final TripsRepository _tripsRepository;
  final String? tripId;

  /// Non-null when editing an existing Draft invoice; null when creating one.
  final Invoice? editingInvoice;

  bool get isEditing => editingInvoice != null;

  CreateInvoiceTripState _tripState = CreateInvoiceTripState.loading;
  TripResponse? _trip;
  ApiException? _tripError;

  CreateInvoiceTripState get tripState => _tripState;
  TripResponse? get trip => _trip;
  ApiException? get tripError => _tripError;

  String amountText = '';
  String? notes;

  bool _isSubmitting = false;
  String? _formError;
  Invoice? _result;

  bool get isSubmitting => _isSubmitting;
  String? get formError => _formError;
  Invoice? get result => _result;

  static String _trimZeros(double value) {
    final text = value.toString();
    return text.endsWith('.0') ? text.substring(0, text.length - 2) : text;
  }

  /// Retries fetching the linked trip after a failed load (create mode only).
  Future<void> retryLoadTrip() => _loadTrip();

  Future<void> _loadTrip() async {
    _tripState = CreateInvoiceTripState.loading;
    notifyListeners();
    try {
      _trip = await _tripsRepository.getTripById(tripId!);
      if (_trip!.agreedPrice != null) {
        amountText = _trimZeros(_trip!.agreedPrice!);
      }
      _tripState = CreateInvoiceTripState.loaded;
    } on ApiException catch (error) {
      _tripError = error;
      _tripState = CreateInvoiceTripState.error;
    }
    notifyListeners();
  }

  void setAmountText(String value) {
    amountText = value;
    notifyListeners();
  }

  void setNotes(String value) {
    notes = value;
    notifyListeners();
  }

  bool get isValid => (double.tryParse(amountText) ?? 0) > 0;

  Future<bool> submit({bool issueImmediately = false}) async {
    final amount = double.tryParse(amountText);
    if (amount == null || amount <= 0) {
      _formError = 'Enter an amount greater than zero.';
      notifyListeners();
      return false;
    }

    _isSubmitting = true;
    _formError = null;
    notifyListeners();
    try {
      final editing = editingInvoice;
      _result = editing != null
          ? await _repository.updateInvoice(editing.invoiceId, amount: amount, notes: notes)
          : await _repository.createInvoice(
              tripId: tripId!,
              amount: amount,
              notes: notes,
              issueImmediately: issueImmediately,
            );
      return true;
    } on ApiException catch (error) {
      _formError = error.message;
      return false;
    } finally {
      _isSubmitting = false;
      notifyListeners();
    }
  }
}
