import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../auth/providers/auth_provider.dart';
import '../data/billing_repository.dart';

class PaymentsScreen extends StatefulWidget {
  const PaymentsScreen({super.key});

  @override
  State<PaymentsScreen> createState() => _PaymentsScreenState();
}

class _PaymentsScreenState extends State<PaymentsScreen> {
  late Future<List<Map<String, dynamic>>> _invoices;
  String? _submittingInvoiceId;

  @override
  void initState() {
    super.initState();
    _invoices = context.read<BillingRepository>().getInvoices();
  }

  void _refresh() => setState(() {
        _invoices = context.read<BillingRepository>().getInvoices();
      });

  bool _canSubmitReceipt(String? status) =>
      status == 'Issued' || status == 'PaymentPending' || status == 'Failed';

  Future<void> _submitReceipt(String invoiceId) async {
    final repo = context.read<BillingRepository>();
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'png', 'jpg', 'jpeg'],
      withData: true,
    );
    final picked = result?.files.single;
    if (picked == null || picked.bytes == null) return;

    setState(() => _submittingInvoiceId = invoiceId);
    try {
      final publicId = await repo.uploadFile(picked.bytes!, picked.name);
      await repo.submitPaymentProof(invoiceId, publicId);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Payment receipt submitted. Awaiting Agency confirmation.')),
      );
      _refresh();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Could not submit receipt: $error')),
      );
    } finally {
      if (mounted) setState(() => _submittingInvoiceId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isShipper = context.watch<AuthProvider>().user?.isShipper ?? false;
    return Scaffold(
      appBar: AppBar(title: const Text('Invoices & Payments')),
      body: FutureBuilder<List<Map<String, dynamic>>>(
        future: _invoices,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(
              child: FilledButton.icon(
                onPressed: _refresh,
                icon: const Icon(Icons.refresh),
                label: const Text('Retry loading invoices'),
              ),
            );
          }
          final invoices = snapshot.data ?? const [];
          if (invoices.isEmpty) {
            return const Center(child: Text('No invoices are available yet.'));
          }

          return RefreshIndicator(
            onRefresh: () async => _refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: invoices.length,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final invoice = invoices[index];
                final invoiceId = invoice['invoiceId'] as String;
                final tripId = invoice['tripId'] as String?;
                final status = invoice['status'] as String?;
                final amount = invoice['totalAmount'] ?? invoice['amount'] ?? 0;
                return Card(
                  child: Padding(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(invoice['invoiceNumber'] as String? ?? invoiceId,
                            style: Theme.of(context).textTheme.titleMedium),
                        const SizedBox(height: 4),
                        Text('LKR $amount · ${status ?? 'Unknown'}'),
                        if (status == 'PaymentPending' && invoice['paymentProofUrl'] != null) ...[
                          const SizedBox(height: 6),
                          Text(
                            'Receipt submitted — awaiting Agency confirmation.',
                            style: Theme.of(context).textTheme.bodySmall,
                          ),
                        ],
                        if (isShipper && _canSubmitReceipt(status)) ...[
                          const SizedBox(height: 12),
                          Align(
                            alignment: Alignment.centerRight,
                            child: FilledButton.icon(
                              onPressed: _submittingInvoiceId == invoiceId
                                  ? null
                                  : () => _submitReceipt(invoiceId),
                              icon: _submittingInvoiceId == invoiceId
                                  ? const SizedBox(
                                      height: 14,
                                      width: 14,
                                      child: CircularProgressIndicator(strokeWidth: 2),
                                    )
                                  : const Icon(Icons.upload_file, size: 18),
                              label: Text(
                                status == 'PaymentPending' ? 'Replace receipt' : 'Submit payment receipt',
                              ),
                            ),
                          ),
                        ],
                        if (tripId != null) ...[
                          const SizedBox(height: 6),
                          Align(
                            alignment: Alignment.centerRight,
                            child: TextButton(
                              onPressed: () => context.push('/payments/dispute', extra: tripId),
                              child: const Text('Raise dispute'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
