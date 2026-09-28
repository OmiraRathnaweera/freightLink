import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
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

/// Read-only claimant view of every dispute related to the caller's trips.
/// The backend is authoritative about relationship/ownership scoping.
class MyDisputesScreen extends StatelessWidget {
  const MyDisputesScreen({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (context) =>
        DisputeListProvider(context.read<DisputeRepository>())..load(),
    child: const _MyDisputesView(),
  );
}

class _MyDisputesView extends StatelessWidget {
  const _MyDisputesView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<DisputeListProvider>();
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppTopBar(
        title: 'Trip Disputes',
        subtitle: 'Status and Admin resolutions',
        showBackButton: true,
        actions: [
          IconButton(
            tooltip: 'Refresh disputes',
            onPressed: provider.state == DisputeListState.loading
                ? null
                : provider.load,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: Column(
        children: [
          _StatusFilters(provider: provider),
          Expanded(child: _DisputeListBody(provider: provider)),
        ],
      ),
    );
  }
}

class _StatusFilters extends StatelessWidget {
  const _StatusFilters({required this.provider});

  final DisputeListProvider provider;

  @override
  Widget build(BuildContext context) {
    final filters = <(String, DisputeStatus?)>[
      ('All', null),
      ('Raised', DisputeStatus.raised),
      ('Under Review', DisputeStatus.underReview),
      ('Resolved', DisputeStatus.resolved),
    ];
    return SizedBox(
      height: 56,
      child: ListView.separated(
        padding: const EdgeInsets.fromLTRB(
          AppConstants.spaceLg,
          10,
          AppConstants.spaceLg,
          8,
        ),
        scrollDirection: Axis.horizontal,
        itemCount: filters.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final filter = filters[index];
          return ChoiceChip(
            label: Text(filter.$1),
            selected: provider.statusFilter == filter.$2,
            onSelected: (_) => provider.setStatusFilter(filter.$2),
          );
        },
      ),
    );
  }
}

class _DisputeListBody extends StatelessWidget {
  const _DisputeListBody({required this.provider});

  final DisputeListProvider provider;

  @override
  Widget build(BuildContext context) {
    switch (provider.state) {
      case DisputeListState.loading:
        return const Center(
          child: CircularProgressIndicator(color: AppColors.primary),
        );
      case DisputeListState.error:
        return ErrorState(
          title: 'Unable to load disputes',
          message:
              provider.error?.message ??
              'Please check your connection and try again.',
          errorCode: provider.error?.code,
          onRetry: provider.load,
        );
      case DisputeListState.empty:
        return RefreshIndicator(
          onRefresh: provider.load,
          color: AppColors.primary,
          child: ListView(
            padding: const EdgeInsets.all(AppConstants.spaceLg),
            children: const [
              SizedBox(height: 100),
              Icon(Icons.balance_outlined, size: 56, color: AppColors.inkMuted),
              SizedBox(height: AppConstants.spaceMd),
              Center(
                child: Text(
                  'No disputes found',
                  style: TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                    color: AppColors.ink,
                  ),
                ),
              ),
              SizedBox(height: AppConstants.spaceSm),
              Center(
                child: Text(
                  'Disputes filed for your trips will appear here.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: AppColors.inkMuted),
                ),
              ),
            ],
          ),
        );
      case DisputeListState.loaded:
        return RefreshIndicator(
          onRefresh: provider.load,
          color: AppColors.primary,
          child: ListView.separated(
            padding: const EdgeInsets.all(AppConstants.spaceLg),
            itemCount: provider.items.length + 1,
            separatorBuilder: (_, _) =>
                const SizedBox(height: AppConstants.spaceMd),
            itemBuilder: (context, index) {
              if (index == provider.items.length) {
                return const Padding(
                  padding: EdgeInsets.only(top: AppConstants.spaceSm),
                  child: Text(
                    'To raise a new dispute, open the relevant invoice from Payments.',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 12, color: AppColors.inkMuted),
                  ),
                );
              }
              return _DisputeCard(dispute: provider.items[index]);
            },
          ),
        );
    }
  }
}

class _DisputeCard extends StatelessWidget {
  const _DisputeCard({required this.dispute});

  final Dispute dispute;

  @override
  Widget build(BuildContext context) => InkWell(
    onTap: () => context.push('/payments/disputes/${dispute.disputeId}'),
    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
    child: SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _Pill(
                label: dispute.category.label,
                background: AppColors.statusNeutralBg,
                foreground: AppColors.ink,
              ),
              const SizedBox(width: AppConstants.spaceSm),
              _Pill(
                label: dispute.status.label,
                background: dispute.status.background,
                foreground: dispute.status.foreground,
              ),
              const Spacer(),
              const Icon(
                Icons.chevron_right_rounded,
                color: AppColors.inkMuted,
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),
          Text(
            dispute.tripRouteSummary ?? 'Trip ${dispute.tripId}',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.ink,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Filed by ${dispute.raisedByName ?? dispute.raisedByRole ?? 'account holder'} · ${AppFormatters.date(dispute.createdAt)}',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 12, color: AppColors.inkMuted),
          ),
        ],
      ),
    ),
  );
}

class _Pill extends StatelessWidget {
  const _Pill({
    required this.label,
    required this.background,
    required this.foreground,
  });

  final String label;
  final Color background;
  final Color foreground;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
    decoration: BoxDecoration(
      color: background,
      borderRadius: BorderRadius.circular(99),
    ),
    child: Text(
      label,
      style: TextStyle(
        fontSize: 11,
        fontWeight: FontWeight.w700,
        color: foreground,
      ),
    ),
  );
}
