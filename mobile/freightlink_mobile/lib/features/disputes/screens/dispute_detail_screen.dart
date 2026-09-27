import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/section_card.dart';
import '../data/dispute_repository.dart';
import '../models/dispute.dart';
import '../providers/dispute_provider.dart';

/// Read-only claimant detail. Admin actions deliberately do not exist in the
/// mobile application; this page only exposes submitted facts and resolution.
class DisputeDetailScreen extends StatelessWidget {
  const DisputeDetailScreen({super.key, required this.disputeId});

  final String disputeId;

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (context) =>
        DisputeDetailProvider(context.read<DisputeRepository>(), disputeId)
          ..fetch(),
    child: const _DisputeDetailView(),
  );
}

class _DisputeDetailView extends StatelessWidget {
  const _DisputeDetailView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<DisputeDetailProvider>();
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Dispute Detail',
        showBackButton: true,
        actions: [
          IconButton(
            tooltip: 'Refresh dispute',
            onPressed: provider.state == DisputeDetailState.loading
                ? null
                : provider.fetch,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: switch (provider.state) {
        DisputeDetailState.loading => const Center(
          child: CircularProgressIndicator(color: AppColors.primary),
        ),
        DisputeDetailState.error => ErrorState(
          title: 'Unable to load dispute',
          message: provider.error?.message ?? 'Please try again.',
          errorCode: provider.error?.code,
          onRetry: provider.fetch,
        ),
        DisputeDetailState.loaded => _DisputeDetailContent(
          dispute: provider.dispute!,
        ),
      },
    );
  }
}

class _DisputeDetailContent extends StatelessWidget {
  const _DisputeDetailContent({required this.dispute});

  final Dispute dispute;

  @override
  Widget build(BuildContext context) => RefreshIndicator(
    onRefresh: () => context.read<DisputeDetailProvider>().fetch(),
    color: AppColors.primary,
    child: ListView(
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      children: [
        SectionCard(
          title: 'Dispute status',
          icon: Icons.balance_outlined,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _StatusChip(status: dispute.status),
              const SizedBox(height: AppConstants.spaceMd),
              _row('Category', dispute.category.label),
              _row('Trip', dispute.tripId),
              _row(
                'Route',
                dispute.tripRouteSummary ?? 'Route details unavailable',
              ),
              if (dispute.carrierAgencyName != null)
                _row('Carrier', dispute.carrierAgencyName!),
              if (dispute.vehicleRegistrationNo != null)
                _row('Vehicle', dispute.vehicleRegistrationNo!),
            ],
          ),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        SectionCard(
          title: 'Your dispute statement',
          icon: Icons.description_outlined,
          child: Text(
            dispute.description,
            style: const TextStyle(
              fontSize: 14,
              height: 1.45,
              color: AppColors.ink,
            ),
          ),
        ),
        const SizedBox(height: AppConstants.spaceLg),
        SectionCard(
          title: 'Record history',
          icon: Icons.history_rounded,
          child: Column(
            children: [
              _row(
                'Filed by',
                dispute.raisedByName ??
                    dispute.raisedByRole ??
                    dispute.raisedByUserId,
              ),
              _row('Raised', AppFormatters.dateTime(dispute.createdAt)),
              _row(
                'Last updated',
                AppFormatters.dateTime(dispute.updatedAt ?? dispute.createdAt),
              ),
            ],
          ),
        ),
        if (dispute.status == DisputeStatus.resolved &&
            dispute.resolution != null) ...[
          const SizedBox(height: AppConstants.spaceLg),
          _ResolutionCard(resolution: dispute.resolution!),
        ],
        const SizedBox(height: AppConstants.spaceXl),
        const Text(
          'This record is read-only after submission. Refresh to see the latest Admin decision.',
          textAlign: TextAlign.center,
          style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
        ),
      ],
    ),
  );

  Widget _row(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 5),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 104,
          child: Text(
            label,
            style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontSize: 13,
              color: AppColors.ink,
              fontWeight: FontWeight.w500,
            ),
          ),
        ),
      ],
    ),
  );
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final DisputeStatus status;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
    decoration: BoxDecoration(
      color: status.background,
      borderRadius: BorderRadius.circular(99),
    ),
    child: Text(
      status.label,
      style: TextStyle(fontWeight: FontWeight.w700, color: status.foreground),
    ),
  );
}

class _ResolutionCard extends StatelessWidget {
  const _ResolutionCard({required this.resolution});

  final DisputeResolution resolution;

  @override
  Widget build(BuildContext context) => Container(
    decoration: BoxDecoration(
      color: AppColors.statusSuccessBg,
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      border: Border.all(
        color: AppColors.statusSuccessFg.withValues(alpha: 0.3),
      ),
    ),
    padding: const EdgeInsets.all(AppConstants.spaceLg),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Row(
          children: [
            Icon(Icons.check_circle_outline, color: AppColors.statusSuccessFg),
            SizedBox(width: AppConstants.spaceSm),
            Text(
              'ADMIN RESOLUTION',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                letterSpacing: 0.4,
                color: AppColors.statusSuccessFg,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppConstants.spaceMd),
        Text(
          'Outcome: ${resolution.outcome.label}',
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            color: AppColors.ink,
          ),
        ),
        const SizedBox(height: AppConstants.spaceSm),
        Text(
          resolution.notes,
          style: const TextStyle(
            fontSize: 14,
            height: 1.45,
            color: AppColors.ink,
          ),
        ),
        const SizedBox(height: AppConstants.spaceMd),
        Text(
          'Resolved ${AppFormatters.dateTime(resolution.resolvedAt)}',
          style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
        ),
      ],
    ),
  );
}
