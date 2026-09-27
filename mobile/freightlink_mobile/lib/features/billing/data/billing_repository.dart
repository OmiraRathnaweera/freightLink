import '../../../core/network/api_client.dart';
import '../models/invoice.dart';

/// API boundary for the mobile invoice/payment flow — both the Shipper's
/// payment-receipt-upload path and the Agency Staff's full invoice lifecycle
/// (create/edit/issue/confirm-payment/void), mirroring the backend's
/// `InvoicesController` and the web app's `features/billing/api/invoiceApi.js`.
class BillingRepository {
  BillingRepository(this._client);

  final ApiClient _client;

  Future<List<Invoice>> getInvoices() async {
    final response = await _client.get('/invoices', query: {'page': 1, 'pageSize': 100})
        as Map<String, dynamic>;
    return (response['items'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>()
        .map(Invoice.fromJson)
        .toList();
  }

  Future<Invoice> getInvoiceById(String invoiceId) async {
    final response = await _client.get('/invoices/$invoiceId') as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// Uploads a payment receipt file via the shared upload endpoint and returns its publicId.
  Future<String> uploadFile(List<int> bytes, String fileName) async {
    final response = await _client.postMultipart(
      '/files/single',
      fileBytes: bytes,
      filename: fileName,
    ) as Map<String, dynamic>;
    return response['publicId'] as String;
  }

  /// Submits an already-uploaded file as proof of payment for an invoice. Advances the invoice to
  /// PaymentPending for Agency review.
  Future<Invoice> submitPaymentProof(String invoiceId, String publicId) async {
    final response = await _client.post(
      '/invoices/$invoiceId/payment-proof',
      body: {'publicId': publicId},
    ) as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// Creates a Draft or Issued invoice against a `Delivered` trip (Agency Staff only). This system
  /// has no direct customers and no manual price negotiation at invoicing time — every invoice
  /// bills the trip's shipper at a single amount (the backend defaults it from the accepted
  /// assignment's agreed price when omitted), so there is no line-item list here.
  Future<Invoice> createInvoice({
    required String tripId,
    double? amount,
    String? notes,
    bool issueImmediately = false,
  }) async {
    final response = await _client.post(
      '/invoices',
      body: {
        'tripId': tripId,
        if (amount != null) 'amount': amount,
        if (notes != null) 'notes': notes,
        'issueImmediately': issueImmediately,
      },
    ) as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// Edits a Draft invoice's amount/notes (Agency Staff only).
  Future<Invoice> updateInvoice(String invoiceId, {required double amount, String? notes}) async {
    final response = await _client.put(
      '/invoices/$invoiceId',
      body: {
        'amount': amount,
        if (notes != null) 'notes': notes,
      },
    ) as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// Draft -> Issued (Agency Staff only).
  Future<Invoice> issueInvoice(String invoiceId) async {
    final response = await _client.post('/invoices/$invoiceId/issue') as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// PaymentPending -> Paid, after reviewing the Shipper's submitted proof (Agency Staff only).
  Future<Invoice> confirmPayment(String invoiceId) async {
    final response = await _client.post('/invoices/$invoiceId/confirm-payment') as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  /// Voids an invoice with a mandatory reason (Agency Staff only).
  Future<Invoice> voidInvoice(String invoiceId, String reason) async {
    final response = await _client.post(
      '/invoices/$invoiceId/void',
      body: {'voidReason': reason},
    ) as Map<String, dynamic>;
    return Invoice.fromJson(response);
  }

  Future<void> raiseDispute({
    required String tripId,
    required String category,
    required String description,
  }) async {
    await _client.post('/disputes', body: {
      'tripId': tripId,
      'category': category,
      'description': description,
    });
  }
}
