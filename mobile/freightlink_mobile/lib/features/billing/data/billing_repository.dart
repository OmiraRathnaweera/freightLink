import '../../../core/network/api_client.dart';

/// API boundary for the mobile Shipper invoice and payment-receipt-upload flow.
class BillingRepository {
  BillingRepository(this._client);

  final ApiClient _client;

  Future<List<Map<String, dynamic>>> getInvoices() async {
    final response = await _client.get('/invoices', query: {'page': 1, 'pageSize': 100})
        as Map<String, dynamic>;
    return (response['items'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>();
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
  Future<Map<String, dynamic>> submitPaymentProof(String invoiceId, String publicId) async {
    return await _client.post(
      '/invoices/$invoiceId/payment-proof',
      body: {'publicId': publicId},
    ) as Map<String, dynamic>;
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
