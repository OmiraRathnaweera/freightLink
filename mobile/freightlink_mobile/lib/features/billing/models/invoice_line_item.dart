/// One billable line item on an invoice, mirroring `InvoiceLineItemDto`.
class InvoiceLineItem {
  const InvoiceLineItem({
    this.invoiceLineItemId,
    required this.description,
    required this.quantity,
    required this.unitPrice,
    this.taxRate = 0,
  });

  factory InvoiceLineItem.fromJson(Map<String, dynamic> json) {
    return InvoiceLineItem(
      invoiceLineItemId: json['invoiceLineItemId'] as String?,
      description: json['description'] as String? ?? '',
      quantity: (json['quantity'] as num?)?.toDouble() ?? 1,
      unitPrice: (json['unitPrice'] as num?)?.toDouble() ?? 0,
      taxRate: (json['taxRate'] as num?)?.toDouble() ?? 0,
    );
  }

  final String? invoiceLineItemId;
  final String description;
  final double quantity;
  final double unitPrice;
  final double taxRate;

  /// quantity * unitPrice * (1 + taxRate), matching the backend's own line total.
  double get amount => quantity * unitPrice * (1 + taxRate);
}
