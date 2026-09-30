import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/section_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../data/billing_repository.dart';
import '../models/invoice.dart';
import '../models/invoice_status.dart';
import '../providers/invoice_detail_provider.dart';
import '../widgets/invoice_status_pill.dart';
import 'create_invoice_screen.dart';

/// Full invoice detail — the single screen both Shipper and Agency Staff
/// land on, mirroring the web app's `InvoiceDetailsDrawer` pattern: role AND
/// current status together gate which actions render, never role alone.
class InvoiceDetailScreen extends StatelessWidget {
  const InvoiceDetailScreen({super.key, required this.invoiceId});

  final String invoiceId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          InvoiceDetailProvider(context.read<BillingRepository>(), invoiceId)..fetch(),
      child: const _InvoiceDetailView(),
    );
  }
}

class _InvoiceDetailView extends StatefulWidget {
  const _InvoiceDetailView();

  @override
  State<_InvoiceDetailView> createState() => _InvoiceDetailViewState();
}

class _InvoiceDetailViewState extends State<_InvoiceDetailView> {
  bool _isUploadingProof = false;

  Future<void> _submitPaymentProof(InvoiceDetailProvider provider) async {
    final result = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'png', 'jpg', 'jpeg'],
    );
    final picked = result.isEmpty ? null : result.first;
    if (picked == null) return;
    if (!mounted) return;

    setState(() => _isUploadingProof = true);
    try {
      final repository = context.read<BillingRepository>();
      final publicId = await repository.uploadFile(await picked.readAsBytes(), picked.name);
      final success = await provider.submitPaymentProof(publicId);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            success
                ? 'Payment receipt submitted. Awaiting Agency confirmation.'
                : 'Could not submit receipt: ${provider.error?.message ?? 'unknown error'}',
          ),
        ),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Could not submit receipt: $error')));
    } finally {
      if (mounted) setState(() => _isUploadingProof = false);
    }
  }

  Future<void> _issue(InvoiceDetailProvider provider) async {
    final success = await provider.issue();
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(success ? 'Invoice issued.' : provider.error?.message ?? 'Could not issue invoice.')),
    );
  }

  Future<void> _confirmPayment(InvoiceDetailProvider provider) async {
    final success = await provider.confirmPayment();
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(success ? 'Payment confirmed. Invoice closed as Paid.' : provider.error?.message ?? 'Could not confirm payment.'),
      ),
    );
  }

  Future<void> _voidInvoice(InvoiceDetailProvider provider) async {
    final reasonController = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Void Invoice'),
        content: TextField(
          controller: reasonController,
          decoration: const InputDecoration(labelText: 'Reason for voiding', hintText: 'Required'),
          maxLines: 3,
          maxLength: 1000,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(dialogContext), child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, reasonController.text.trim()),
            child: const Text('Confirm Void'),
          ),
        ],
      ),
    );
    if (reason == null || reason.isEmpty) return;

    final success = await provider.voidInvoice(reason);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(success ? 'Invoice voided.' : provider.error?.message ?? 'Could not void invoice.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<InvoiceDetailProvider>();
    final user = context.watch<AuthProvider>().user;
    final isAgencyStaff = user?.isAgencyStaff ?? false;
    final isShipper = user?.isShipper ?? false;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Invoice Detail',
        showBackButton: true,
        actions: [IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: provider.fetch)],
      ),
      body: SafeArea(
        child: Builder(
          builder: (context) {
            if (provider.state == InvoiceDetailState.loading) {
              return const Center(child: CircularProgressIndicator(color: AppColors.primary));
            }
            if (provider.state == InvoiceDetailState.error) {
              return ErrorState(
                title: 'Failed to Load Invoice',
                message: provider.error?.message ?? 'Please try again.',
                onRetry: provider.fetch,
              );
            }

            final invoice = provider.invoice!;
            final canEdit = isAgencyStaff && invoice.status.isEditable;
            final canIssue = isAgencyStaff && invoice.status.canIssue;
            final canVoid = isAgencyStaff && invoice.status.isVoidable;
            final canConfirmPayment =
                isAgencyStaff && invoice.status == InvoiceStatus.paymentPending && invoice.hasPaymentProof;
            final canSubmitProof = isShipper && invoice.status.canSubmitPaymentProof;

            return ListView(
              padding: const EdgeInsets.all(AppConstants.spaceLg),
              children: [
                _buildOverviewCard(invoice),
                const SizedBox(height: AppConstants.spaceLg),
                _buildLineItemsCard(invoice),
                if (invoice.status == InvoiceStatus.void_ && invoice.auditTrail?.voidReason != null) ...[
                  const SizedBox(height: AppConstants.spaceLg),
                  _buildVoidCard(invoice),
                ],
                if (canEdit || canIssue || canVoid || canConfirmPayment || canSubmitProof) ...[
                  const SizedBox(height: AppConstants.spaceLg),
                  _buildActions(
                    context: context,
                    provider: provider,
                    canEdit: canEdit,
                    canIssue: canIssue,
                    canVoid: canVoid,
                    canConfirmPayment: canConfirmPayment,
                    canSubmitProof: canSubmitProof,
                  ),
                ],
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _buildOverviewCard(Invoice invoice) {
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  invoice.invoiceNumber,
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700, color: AppColors.ink),
                ),
              ),
              InvoiceStatusPill(status: invoice.status),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),
          _overviewRow('Recipient', invoice.recipientName ?? 'Direct'),
          _overviewRow('Subtotal', AppFormatters.currency(invoice.subtotal)),
          _overviewRow('Tax', AppFormatters.currency(invoice.taxTotal)),
          _overviewRow('Discount', AppFormatters.currency(invoice.discountTotal)),
          _overviewRow('Total', AppFormatters.currency(invoice.totalAmount), bold: true),
          if (invoice.issuedAt != null) _overviewRow('Issued', AppFormatters.date(invoice.issuedAt!)),
          if (invoice.dueDate != null) _overviewRow('Due', invoice.dueDate!),
          if (invoice.paidAt != null) _overviewRow('Paid', AppFormatters.date(invoice.paidAt!)),
          if (invoice.hasPaymentProof) _overviewRow('Payment Proof', 'Submitted'),
        ],
      ),
    );
  }

  Widget _overviewRow(String label, String value, {bool bold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(fontSize: 13, color: AppColors.inkMuted)),
          const SizedBox(width: AppConstants.spaceSm),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 13, fontWeight: bold ? FontWeight.w700 : FontWeight.w500, color: AppColors.ink),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLineItemsCard(Invoice invoice) {
    return SectionCard(
      title: 'Line Items',
      icon: Icons.receipt_long_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: invoice.lineItems.isEmpty
            ? [const Text('No line items recorded.', style: TextStyle(fontSize: 13, color: AppColors.inkMuted))]
            : invoice.lineItems
                  .map(
                    (item) => Padding(
                      padding: const EdgeInsets.symmetric(vertical: 6),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Expanded(
                            child: Text(
                              '${item.description} (${item.quantity} × ${AppFormatters.currency(item.unitPrice)})',
                              style: const TextStyle(fontSize: 13, color: AppColors.ink),
                            ),
                          ),
                          Text(
                            AppFormatters.currency(item.amount),
                            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: AppColors.ink),
                          ),
                        ],
                      ),
                    ),
                  )
                  .toList(),
      ),
    );
  }

  Widget _buildVoidCard(Invoice invoice) {
    return SectionCard(
      title: 'Void Details',
      icon: Icons.block_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Reason: ${invoice.auditTrail?.voidReason}', style: const TextStyle(fontSize: 13, color: AppColors.ink)),
          if (invoice.auditTrail?.voidedByName != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Voided by ${invoice.auditTrail!.voidedByName}',
                style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildActions({
    required BuildContext context,
    required InvoiceDetailProvider provider,
    required bool canEdit,
    required bool canIssue,
    required bool canVoid,
    required bool canConfirmPayment,
    required bool canSubmitProof,
  }) {
    final isBusy = provider.isSubmittingAction || _isUploadingProof;
    return SectionCard(
      title: 'Actions',
      icon: Icons.flash_on_outlined,
      child: Wrap(
        spacing: AppConstants.spaceSm,
        runSpacing: AppConstants.spaceSm,
        children: [
          if (canEdit)
            OutlinedButton.icon(
              onPressed: isBusy
                  ? null
                  : () async {
                      await Navigator.of(context).push(
                        MaterialPageRoute(
                          builder: (_) => CreateInvoiceScreen(editingInvoice: provider.invoice),
                        ),
                      );
                      if (context.mounted) provider.fetch();
                    },
              icon: const Icon(Icons.edit_outlined, size: 18),
              label: const Text('Edit Draft'),
            ),
          if (canIssue)
            FilledButton.icon(
              onPressed: isBusy ? null : () => _issue(provider),
              icon: const Icon(Icons.send_outlined, size: 18),
              label: const Text('Issue Invoice'),
            ),
          if (canConfirmPayment)
            FilledButton.icon(
              onPressed: isBusy ? null : () => _confirmPayment(provider),
              icon: const Icon(Icons.check_circle_outline, size: 18),
              label: const Text('Confirm Payment'),
            ),
          if (canSubmitProof)
            FilledButton.icon(
              onPressed: isBusy ? null : () => _submitPaymentProof(provider),
              icon: _isUploadingProof
                  ? const SizedBox(height: 14, width: 14, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.upload_file_outlined, size: 18),
              label: Text(
                provider.invoice?.status == InvoiceStatus.paymentPending ? 'Replace Receipt' : 'Submit Payment Receipt',
              ),
            ),
          if (canVoid)
            OutlinedButton.icon(
              onPressed: isBusy ? null : () => _voidInvoice(provider),
              style: OutlinedButton.styleFrom(foregroundColor: AppColors.statusErrorFg),
              icon: const Icon(Icons.block_outlined, size: 18),
              label: const Text('Void Invoice'),
            ),
        ],
      ),
    );
  }
}
