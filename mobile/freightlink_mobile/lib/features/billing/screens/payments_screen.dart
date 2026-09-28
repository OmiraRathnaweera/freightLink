import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
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
import '../providers/invoice_list_provider.dart';
import '../widgets/invoice_status_pill.dart';

/// Invoices & Payments tab. The backend already scopes the list by role, so
/// this one screen serves both: a Shipper sees their own invoices with the
/// existing payment-receipt-upload action, and Agency Staff sees invoices
/// tied to their agency's trips with a full lifecycle entry point (tapping a
/// row opens [InvoiceDetailScreen] for create/edit/issue/confirm/void
/// actions) — previously Agency Staff had no invoice actions on mobile at
/// all, only a read-only list.
class PaymentsScreen extends StatelessWidget {
  const PaymentsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          InvoiceListProvider(context.read<BillingRepository>())..load(),
      child: const _PaymentsView(),
    );
  }
}

class _PaymentsView extends StatelessWidget {
  const _PaymentsView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<InvoiceListProvider>();
    final isShipper = context.watch<AuthProvider>().user?.isShipper ?? false;
    final isAgencyStaff =
        context.watch<AuthProvider>().user?.isAgencyStaff ?? false;

    return Scaffold(
      appBar: AppTopBar(
        title: 'Invoices & Payments',
        leading: const AppAvatar(),
        actions: [
          IconButton(
            tooltip: 'Trip disputes',
            onPressed: () => context.push('/payments/disputes'),
            icon: const Icon(Icons.balance_outlined),
          ),
          const NotificationBellButton(),
        ],
      ),
      body: Builder(
        builder: (context) {
          switch (provider.state) {
            case InvoiceListState.loading:
              return const Center(
                child: CircularProgressIndicator(color: AppColors.primary),
              );
            case InvoiceListState.error:
              return ErrorState(
                title: 'Unable to load invoices',
                message:
                    provider.error?.message ??
                    'Please check your connection and try again.',
                onRetry: provider.load,
              );
            case InvoiceListState.empty:
              return RefreshIndicator(
                onRefresh: provider.load,
                color: AppColors.primary,
                child: ListView(
                  padding: const EdgeInsets.all(AppConstants.spaceLg),
                  children: [
                    const _DisputesEntryCard(),
                    const SizedBox(height: 120),
                    const Center(child: Text('No invoices are available yet.')),
                  ],
                ),
              );
            case InvoiceListState.loaded:
              return RefreshIndicator(
                onRefresh: provider.load,
                color: AppColors.primary,
                child: ListView.separated(
                  padding: const EdgeInsets.all(AppConstants.spaceLg),
                  itemCount: provider.items.length + 1,
                  separatorBuilder: (_, _) =>
                      const SizedBox(height: AppConstants.spaceMd),
                  itemBuilder: (context, index) {
                    if (index == 0) return const _DisputesEntryCard();
                    final invoice = provider.items[index - 1];
                    return _InvoiceCard(
                      invoice: invoice,
                      isShipper: isShipper,
                      isAgencyStaff: isAgencyStaff,
                    );
                  },
                ),
              );
          }
        },
      ),
    );
  }
}

/// Prominent, labeled entry point — kept above invoices so Agency Staff do
/// not need to discover the dispute list through the app-bar icon alone.
class _DisputesEntryCard extends StatelessWidget {
  const _DisputesEntryCard();

  @override
  Widget build(BuildContext context) => SectionCard(
    child: Row(
      children: [
        Container(
          width: 42,
          height: 42,
          decoration: BoxDecoration(
            color: AppColors.statusMatchedBg,
            borderRadius: BorderRadius.circular(AppConstants.radiusSm),
          ),
          child: const Icon(
            Icons.balance_outlined,
            color: AppColors.statusMatchedFg,
          ),
        ),
        const SizedBox(width: AppConstants.spaceMd),
        const Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Trip Disputes',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: AppColors.ink,
                ),
              ),
              SizedBox(height: 2),
              Text(
                'Track claims and Admin resolutions',
                style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
              ),
            ],
          ),
        ),
        TextButton(
          onPressed: () => context.push('/payments/disputes'),
          child: const Text('View'),
        ),
      ],
    ),
  );
}

class _InvoiceCard extends StatelessWidget {
  const _InvoiceCard({
    required this.invoice,
    required this.isShipper,
    required this.isAgencyStaff,
  });

  final Invoice invoice;
  final bool isShipper;
  final bool isAgencyStaff;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: () => context.push('/payments/${invoice.invoiceId}'),
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: SectionCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    invoice.invoiceNumber,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: AppColors.ink,
                    ),
                  ),
                ),
                const SizedBox(width: AppConstants.spaceSm),
                InvoiceStatusPill(status: invoice.status),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              AppFormatters.currency(invoice.totalAmount),
              style: const TextStyle(fontSize: 13, color: AppColors.inkMuted),
            ),
            if (invoice.status == InvoiceStatus.paymentPending &&
                invoice.hasPaymentProof) ...[
              const SizedBox(height: 6),
              const Text(
                'Receipt submitted — awaiting Agency confirmation.',
                style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
              ),
            ],
            const SizedBox(height: 10),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                if (invoice.tripId != null)
                  TextButton(
                    onPressed: () => context.push(
                      '/payments/dispute',
                      extra: invoice.tripId,
                    ),
                    child: const Text('Raise dispute'),
                  ),
                TextButton(
                  onPressed: () =>
                      context.push('/payments/${invoice.invoiceId}'),
                  child: Text(isAgencyStaff ? 'Manage' : 'View details'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
