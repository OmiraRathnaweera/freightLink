import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/empty_state.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/status_pill.dart';
import '../data/trips_repository.dart';
import '../models/job_proposal.dart';
import 'accept_load_assign_screen.dart';

/// Job proposal inbox for Agency Staff (GET /api/v1/assignments).
/// Displays incoming AI load matches waiting for agency accept/assign decision.
class JobProposalsScreen extends StatefulWidget {
  const JobProposalsScreen({super.key});

  @override
  State<JobProposalsScreen> createState() => _JobProposalsScreenState();
}

class _JobProposalsScreenState extends State<JobProposalsScreen> {
  ProposalStatus? _selectedStatus = ProposalStatus.proposed;
  List<JobProposal> _proposals = [];
  bool _isLoading = true;
  String? _errorMessage;

  final _currencyFormat = NumberFormat.currency(
    symbol: 'LKR ',
    decimalDigits: 2,
  );

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _fetchProposals());
  }

  Future<void> _fetchProposals() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    final repository = context.read<TripsRepository>();

    try {
      final paged = await repository.getProposals(
        status: _selectedStatus,
        pageSize: 50,
      );
      if (!mounted) return;
      setState(() {
        _proposals = paged.items;
        _isLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _errorMessage = e is ApiException ? e.message : 'Failed to load proposals.';
      });
    }
  }

  void _onStatusFilterSelected(ProposalStatus? status) {
    if (_selectedStatus == status) return;
    setState(() => _selectedStatus = status);
    _fetchProposals();
  }

  Future<void> _openProposal(JobProposal proposal) async {
    final result = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => AcceptLoadAssignScreen(proposal: proposal),
      ),
    );

    if (result == true || result == false) {
      _fetchProposals();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(
        title: 'Job Proposals',
        subtitle: 'Review & assign agency fleet to AI-matched loads',
        leading: const AppAvatar(),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh Proposals',
            onPressed: _fetchProposals,
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            _buildFilterChips(),
            Expanded(
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : _errorMessage != null
                      ? ErrorState(
                          title: 'Failed to Load Proposals',
                          message: _errorMessage!,
                          onRetry: _fetchProposals,
                        )
                      : _proposals.isEmpty
                          ? EmptyState(
                              title: 'No Job Proposals',
                              message: _selectedStatus == ProposalStatus.proposed
                                  ? 'No pending proposals right now. Newly approved loads will appear here.'
                                  : 'No assignments found matching this filter.',
                              icon: Icons.assignment_outlined,
                            )
                          : RefreshIndicator(
                              onRefresh: _fetchProposals,
                              child: ListView.separated(
                                padding: const EdgeInsets.all(AppConstants.spaceLg),
                                itemCount: _proposals.length,
                                separatorBuilder: (_, _) =>
                                    const SizedBox(height: AppConstants.spaceMd),
                                itemBuilder: (context, index) {
                                  return _buildProposalCard(_proposals[index]);
                                },
                              ),
                            ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFilterChips() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(
        horizontal: AppConstants.spaceLg,
        vertical: AppConstants.spaceSm,
      ),
      child: Row(
        children: [
          _buildFilterChip('Action Required', ProposalStatus.proposed),
          const SizedBox(width: AppConstants.spaceSm),
          _buildFilterChip('Accepted', ProposalStatus.accepted),
          const SizedBox(width: AppConstants.spaceSm),
          _buildFilterChip('Declined', ProposalStatus.declined),
          const SizedBox(width: AppConstants.spaceSm),
          _buildFilterChip('All Proposals', null),
        ],
      ),
    );
  }

  Widget _buildFilterChip(String label, ProposalStatus? status) {
    final isSelected = _selectedStatus == status;
    return FilterChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (_) => _onStatusFilterSelected(status),
      backgroundColor: AppColors.surface,
      selectedColor: AppColors.statusMatchedBg,
      labelStyle: TextStyle(
        fontSize: 13,
        fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
        color: isSelected ? AppColors.statusMatchedFg : AppColors.inkMuted,
      ),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppConstants.radiusSm),
        side: BorderSide(
          color: isSelected ? AppColors.statusMatchedFg : AppColors.border,
        ),
      ),
      showCheckmark: false,
    );
  }

  Widget _buildProposalCard(JobProposal proposal) {
    final priceStr = _currencyFormat.format(proposal.proposedPrice);
    final distanceStr = proposal.routedDistanceKm != null
        ? '${proposal.routedDistanceKm!.toStringAsFixed(1)} km'
        : null;

    final weightStr = proposal.weightKg != null
        ? '${proposal.weightKg!.toStringAsFixed(0)} kg'
        : null;

    return InkWell(
      onTap: () => _openProposal(proposal),
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
          border: Border.all(color: AppColors.border),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.02),
              blurRadius: 4,
              offset: const Offset(0, 2),
            ),
          ],
        ),
        padding: const EdgeInsets.all(AppConstants.spaceLg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header: Ref Code + Status Pill
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  proposal.referenceCode ?? proposal.loadId.substring(0, 8).toUpperCase(),
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.4,
                    color: AppColors.inkMuted,
                  ),
                ),
                StatusPill(
                  label: proposal.status.displayName,
                  foreground: proposal.isProposed
                      ? AppColors.statusMatchedFg
                      : proposal.isAccepted
                          ? AppColors.statusSuccessFg
                          : AppColors.statusErrorFg,
                  background: proposal.isProposed
                      ? AppColors.statusMatchedBg
                      : proposal.isAccepted
                          ? AppColors.statusSuccessBg
                          : AppColors.statusErrorBg,
                ),
              ],
            ),
            const SizedBox(height: 8),

            // Cargo Description
            Text(
              proposal.cargoDescription,
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: AppColors.ink,
              ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
            const SizedBox(height: 6),

            // Route summary
            Row(
              children: [
                const Icon(Icons.arrow_forward_rounded, size: 14, color: AppColors.inkMuted),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    '${proposal.pickupAddress} → ${proposal.dropoffAddress}',
                    style: const TextStyle(fontSize: 13, color: AppColors.inkMuted),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            const Divider(height: AppConstants.spaceLg, color: AppColors.border),

            // Metrics & Price Row
            Row(
              children: [
                if (weightStr != null) ...[
                  const Icon(Icons.scale_rounded, size: 14, color: AppColors.inkMuted),
                  const SizedBox(width: 4),
                  Text(weightStr, style: const TextStyle(fontSize: 12, color: AppColors.ink)),
                  const SizedBox(width: 12),
                ],
                if (distanceStr != null) ...[
                  const Icon(Icons.straighten_rounded, size: 14, color: AppColors.inkMuted),
                  const SizedBox(width: 4),
                  Text(distanceStr, style: const TextStyle(fontSize: 12, color: AppColors.ink)),
                ],
                const Spacer(),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    const Text(
                      'PROPOSED PAYOUT',
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.w700,
                        letterSpacing: 0.4,
                        color: AppColors.inkMuted,
                      ),
                    ),
                    Text(
                      priceStr,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w800,
                        color: AppColors.ink,
                      ),
                    ),
                  ],
                ),
              ],
            ),

            if (proposal.isProposed) ...[
              const SizedBox(height: AppConstants.spaceMd),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                    ),
                  ),
                  icon: const Icon(Icons.check_circle_outline_rounded, size: 16),
                  label: const Text('Review & Assign Fleet'),
                  onPressed: () => _openProposal(proposal),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
