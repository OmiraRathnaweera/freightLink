import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/section_card.dart';
import '../../auth/providers/auth_provider.dart';
import '../../trips/data/trips_repository.dart';
import '../data/billing_repository.dart';
import '../models/invoice.dart';
import '../providers/create_invoice_provider.dart';

/// Create Invoice (Agency Staff only), entered from a `Delivered` trip's detail screen with that
/// trip's id — or Edit Invoice, entered from Invoice Detail for a Draft, in which case
/// [editingInvoice] is supplied and [tripId] is ignored. This system has no direct customers and no
/// manual price negotiation at invoicing time: every invoice bills the trip's shipper at a single
/// amount, defaulted from the trip's already-agreed job price — mirroring the web app's
/// `InvoiceFormModal`, there is no line-item list, currency choice, discount, or due date here.
///
/// This route falls under the shared `/payments/` prefix both Shipper and Agency Staff can reach,
/// so — unlike `/dashboard`'s per-role widget switch — it needs its own guard here rather than
/// relying on `app_router.dart`'s coarser, prefix-only allowlist.
class CreateInvoiceScreen extends StatelessWidget {
  const CreateInvoiceScreen({super.key, this.tripId, this.editingInvoice});

  final String? tripId;
  final Invoice? editingInvoice;

  @override
  Widget build(BuildContext context) {
    final isAgencyStaff = context.watch<AuthProvider>().user?.isAgencyStaff ?? false;
    if (!isAgencyStaff) {
      return Scaffold(
        appBar: AppTopBar(title: editingInvoice != null ? 'Edit Invoice' : 'Create Invoice', showBackButton: true),
        body: const Center(child: Text('Only Agency Staff can manage invoices.')),
      );
    }

    return ChangeNotifierProvider(
      create: (context) => CreateInvoiceProvider(
        context.read<BillingRepository>(),
        context.read<TripsRepository>(),
        tripId: tripId,
        editingInvoice: editingInvoice,
      ),
      child: const _CreateInvoiceView(),
    );
  }
}

class _CreateInvoiceView extends StatelessWidget {
  const _CreateInvoiceView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<CreateInvoiceProvider>();

    Future<void> submit({required bool issueImmediately}) async {
      final success = await provider.submit(issueImmediately: issueImmediately);
      if (!context.mounted) return;
      if (success) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              provider.isEditing
                  ? 'Invoice updated.'
                  : issueImmediately
                  ? 'Invoice created and issued.'
                  : 'Draft invoice saved.',
            ),
          ),
        );
        Navigator.of(context).pop();
      }
    }

    final isLoadingTrip = !provider.isEditing && provider.tripState == CreateInvoiceTripState.loading;
    final tripLoadFailed = !provider.isEditing && provider.tripState == CreateInvoiceTripState.error;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(title: provider.isEditing ? 'Edit Invoice' : 'Create Invoice', showBackButton: true),
      body: SafeArea(
        child: isLoadingTrip
            ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
            : tripLoadFailed
            ? ErrorState(
                title: 'Failed to Load Trip',
                message: provider.tripError?.message ?? 'Please try again.',
                onRetry: provider.retryLoadTrip,
              )
            : ListView(
                padding: const EdgeInsets.all(AppConstants.spaceLg),
                children: [
                  if (provider.formError != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: AppConstants.spaceMd),
                      child: Container(
                        padding: const EdgeInsets.all(AppConstants.spaceMd),
                        decoration: BoxDecoration(
                          color: AppColors.statusErrorBg,
                          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                        ),
                        child: Text(provider.formError!, style: const TextStyle(color: AppColors.statusErrorFg)),
                      ),
                    ),
                  SectionCard(
                    title: provider.isEditing ? 'Invoice' : 'Trip',
                    icon: Icons.local_shipping_outlined,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _infoRow(
                          'Reference',
                          provider.isEditing
                              ? provider.editingInvoice!.invoiceNumber
                              : (provider.trip?.referenceCode ?? '—'),
                        ),
                        _infoRow(
                          'Shipper',
                          provider.isEditing
                              ? (provider.editingInvoice!.recipientName ?? 'Shipper')
                              : (provider.trip?.shipperName ?? '—'),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppConstants.spaceLg),
                  TextFormField(
                    initialValue: provider.amountText,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(labelText: 'Amount (LKR)'),
                    onChanged: provider.setAmountText,
                  ),
                  if (!provider.isEditing && provider.trip?.agreedPrice != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 6),
                      child: Text(
                        'Pre-filled from the agreed job price: ${AppFormatters.currency(provider.trip!.agreedPrice)}',
                        style: const TextStyle(fontSize: 11, color: AppColors.inkMuted),
                      ),
                    ),
                  const SizedBox(height: AppConstants.spaceLg),
                  TextFormField(
                    initialValue: provider.notes,
                    maxLines: 3,
                    maxLength: 2000,
                    decoration: const InputDecoration(labelText: 'Notes (optional)', alignLabelWithHint: true),
                    onChanged: provider.setNotes,
                  ),
                  const SizedBox(height: AppConstants.spaceXl),
                  if (provider.isEditing)
                    FilledButton(
                      onPressed: provider.isSubmitting ? null : () => submit(issueImmediately: false),
                      child: provider.isSubmitting
                          ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Save Changes'),
                    )
                  else ...[
                    FilledButton(
                      onPressed: provider.isSubmitting ? null : () => submit(issueImmediately: true),
                      child: const Text('Create & Issue'),
                    ),
                    const SizedBox(height: AppConstants.spaceSm),
                    OutlinedButton(
                      onPressed: provider.isSubmitting ? null : () => submit(issueImmediately: false),
                      child: const Text('Save as Draft'),
                    ),
                  ],
                ],
              ),
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(fontSize: 13, color: AppColors.inkMuted)),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: AppColors.ink),
            ),
          ),
        ],
      ),
    );
  }
}
