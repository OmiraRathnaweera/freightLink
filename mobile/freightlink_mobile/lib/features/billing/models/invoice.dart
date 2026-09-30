import 'invoice_line_item.dart';
import 'invoice_status.dart';

/// One recorded audit fact on an invoice (who created/updated/voided it),
/// mirroring `InvoiceAuditTrailDto`. Only present on the detail response.
class InvoiceAuditTrail {
  const InvoiceAuditTrail({
    this.createdByName,
    this.updatedByName,
    this.voidedByName,
    this.voidReason,
    this.voidedAt,
  });

  factory InvoiceAuditTrail.fromJson(Map<String, dynamic> json) {
    return InvoiceAuditTrail(
      createdByName: json['createdByName'] as String?,
      updatedByName: json['updatedByName'] as String?,
      voidedByName: json['voidedByName'] as String?,
      voidReason: json['voidReason'] as String?,
      voidedAt: json['voidedAt'] == null ? null : DateTime.parse(json['voidedAt'] as String),
    );
  }

  final String? createdByName;
  final String? updatedByName;
  final String? voidedByName;
  final String? voidReason;
  final DateTime? voidedAt;
}

/// The invoice shape returned by both `GET /invoices` (list) and
/// `GET /invoices/{id}` (detail), mirroring `InvoiceResponseDto`/
/// `InvoiceListItemDto`. The list response omits `lineItems`/`auditTrail`,
/// which is why both are nullable/empty-defaulted here rather than required.
class Invoice {
  const Invoice({
    required this.invoiceId,
    this.tripId,
    required this.invoiceNumber,
    this.recipientId,
    this.recipientName,
    required this.subtotal,
    required this.taxTotal,
    required this.discountTotal,
    required this.totalAmount,
    required this.currency,
    required this.status,
    this.issuedAt,
    this.dueDate,
    this.paidAt,
    this.paymentProofUrl,
    this.paymentProofUploadedAt,
    this.notes,
    required this.createdAt,
    required this.updatedAt,
    this.lineItems = const [],
    this.auditTrail,
  });

  factory Invoice.fromJson(Map<String, dynamic> json) {
    return Invoice(
      invoiceId: json['invoiceId'] as String? ?? json['id'] as String,
      tripId: json['tripId'] as String? ?? json['linkedEntityId'] as String?,
      invoiceNumber: json['invoiceNumber'] as String? ?? '',
      recipientId: json['recipientId'] as String?,
      recipientName: json['recipientName'] as String?,
      subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
      taxTotal: (json['taxTotal'] as num?)?.toDouble() ?? 0,
      discountTotal: (json['discountTotal'] as num?)?.toDouble() ?? 0,
      totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? (json['amount'] as num?)?.toDouble() ?? 0,
      currency: json['currency'] as String? ?? 'LKR',
      status: InvoiceStatus.fromWire(json['status'] as String),
      issuedAt: json['issuedAt'] == null ? null : DateTime.parse(json['issuedAt'] as String),
      dueDate: json['dueDate'] as String?,
      paidAt: json['paidAt'] == null ? null : DateTime.parse(json['paidAt'] as String),
      paymentProofUrl: json['paymentProofUrl'] as String?,
      paymentProofUploadedAt: json['paymentProofUploadedAt'] == null
          ? null
          : DateTime.parse(json['paymentProofUploadedAt'] as String),
      notes: json['notes'] as String?,
      createdAt: DateTime.parse(json['createdAt'] as String),
      updatedAt: DateTime.parse(json['updatedAt'] as String),
      lineItems: (json['lineItems'] as List<dynamic>? ?? const [])
          .cast<Map<String, dynamic>>()
          .map(InvoiceLineItem.fromJson)
          .toList(),
      auditTrail: json['auditTrail'] == null
          ? null
          : InvoiceAuditTrail.fromJson(json['auditTrail'] as Map<String, dynamic>),
    );
  }

  final String invoiceId;
  final String? tripId;
  final String invoiceNumber;
  final String? recipientId;
  final String? recipientName;
  final double subtotal;
  final double taxTotal;
  final double discountTotal;
  final double totalAmount;
  final String currency;
  final InvoiceStatus status;
  final DateTime? issuedAt;
  final String? dueDate;
  final DateTime? paidAt;
  final String? paymentProofUrl;
  final DateTime? paymentProofUploadedAt;
  final String? notes;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<InvoiceLineItem> lineItems;
  final InvoiceAuditTrail? auditTrail;

  bool get hasPaymentProof => paymentProofUrl != null;
}
