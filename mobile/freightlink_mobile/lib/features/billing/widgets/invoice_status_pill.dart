import 'package:flutter/material.dart';

import '../models/invoice_status.dart';

/// Small status badge for an [InvoiceStatus] — the invoice-feature
/// counterpart to `TripStatusPill`.
class InvoiceStatusPill extends StatelessWidget {
  const InvoiceStatusPill({super.key, required this.status});

  final InvoiceStatus status;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: status.background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        status.label.toUpperCase(),
        style: TextStyle(
          fontSize: 10,
          fontWeight: FontWeight.w700,
          letterSpacing: 0.3,
          color: status.foreground,
        ),
      ),
    );
  }
}
