import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/dashboard_action_tile.dart';
import '../../../shared/widgets/dashboard_kpi_card.dart';
import '../../../shared/widgets/error_state.dart';
import '../../auth/providers/auth_provider.dart';
import '../data/loads_repository.dart';
import '../../billing/data/billing_repository.dart';
import '../providers/shipper_dashboard_provider.dart';
import 'post_load_screen.dart';

/// Shipper Dashboard screen — the Shipper counterpart to
/// [AgencyDashboardScreen]: a hero banner, shipment-pipeline KPIs (drawn from
/// [ShipperDashboardProvider]'s per-status load counts), and quick actions
/// (post a load, view all loads, payments & invoices).
class ShipperDashboardScreen extends StatelessWidget {
  const ShipperDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ShipperDashboardProvider(
        context.read<LoadsRepository>(),
        context.read<BillingRepository>(),
      )..loadStats(),
      child: const _ShipperDashboardView(),
    );
  }
}

class _ShipperDashboardView extends StatelessWidget {
  const _ShipperDashboardView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ShipperDashboardProvider>();

    return Scaffold(
      appBar: const AppTopBar(
        title: 'Shipper Dashboard',
        subtitle: 'Shipments & Billing Overview',
        leading: AppAvatar(),
        actions: [NotificationBellButton()],
      ),
      body: Builder(
        builder: (context) {
          if (provider.state == ShipperDashboardState.loading) {
            return const _DashboardSkeleton();
          }

          if (provider.state == ShipperDashboardState.error) {
            return Center(
              child: ErrorState(
                title: 'Unable to load dashboard',
                message: provider.errorMessage ?? 'Please check your connection and try again.',
                onRetry: provider.loadStats,
              ),
            );
          }

          final counts = provider.loadCounts;

          return RefreshIndicator(
            onRefresh: provider.loadStats,
            color: AppColors.primary,
            child: SingleChildScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.symmetric(
                horizontal: AppConstants.spaceLg,
                vertical: AppConstants.spaceMd,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const _ShipperHeroBanner(),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Shipments Overview',
                    icon: Icons.analytics_outlined,
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  _KpiGrid(counts: counts),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Quick Actions',
                    icon: Icons.flash_on_outlined,
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  DashboardActionTile(
                    title: 'Post a Load',
                    subtitle: 'Create a new shipment for carriers to match',
                    icon: Icons.add_box_outlined,
                    onTap: () => _postLoad(context, provider),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'My Loads',
                    subtitle: 'Track every shipment you have posted',
                    icon: Icons.local_shipping_outlined,
                    badgeText: '${counts['total'] ?? 0} Total',
                    onTap: () => context.go('/loads'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Payments & Invoices',
                    subtitle: 'Review invoices and submit payment receipts',
                    icon: Icons.receipt_long_outlined,
                    badgeText: provider.pendingInvoices > 0
                        ? '${provider.pendingInvoices} Due'
                        : 'Up to date',
                    isWarning: provider.pendingInvoices > 0,
                    onTap: () => context.go('/payments'),
                  ),
                  const SizedBox(height: AppConstants.spaceXl),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  Future<void> _postLoad(BuildContext context, ShipperDashboardProvider provider) async {
    final created = await Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => const PostLoadScreen()));
    if (created != null && context.mounted) {
      provider.loadStats();
    }
  }

  Widget _buildSectionHeader({required String title, required IconData icon}) {
    return Row(
      children: [
        Icon(icon, size: 16, color: AppColors.inkMuted),
        const SizedBox(width: AppConstants.spaceSm),
        Text(
          title.toUpperCase(),
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.5,
            color: AppColors.inkMuted,
          ),
        ),
      ],
    );
  }
}

/// Hero card greeting the signed-in Shipper — the Shipper counterpart to
/// Agency Dashboard's `_AgencyHeroBanner`.
class _ShipperHeroBanner extends StatelessWidget {
  const _ShipperHeroBanner();

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final shipperName = user?.fullName.isNotEmpty == true ? user!.fullName : 'Shipper';
    final email = user?.email ?? '';

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: AppColors.primary,
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.inventory_2_outlined,
              color: AppColors.onPrimary,
              size: 24,
            ),
          ),
          const SizedBox(width: AppConstants.spaceMd),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'SHIPPER PORTAL',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.5,
                    color: AppColors.inkMuted,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  shipperName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    color: AppColors.ink,
                  ),
                ),
                if (email.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    email,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// 2x2 Grid of shipment-pipeline KPI cards.
class _KpiGrid extends StatelessWidget {
  const _KpiGrid({required this.counts});

  final Map<String, int> counts;

  @override
  Widget build(BuildContext context) {
    final posted = counts['posted'] ?? 0;
    final matched = counts['matched'] ?? 0;
    final inTransit = counts['inTransit'] ?? 0;
    final delivered = counts['delivered'] ?? 0;

    return GridView.count(
      crossAxisCount: 2,
      crossAxisSpacing: AppConstants.spaceMd,
      mainAxisSpacing: AppConstants.spaceMd,
      childAspectRatio: 1.25,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      children: [
        DashboardKpiCard(
          icon: Icons.assignment_outlined,
          iconBg: AppColors.statusNeutralBg,
          iconFg: AppColors.ink,
          value: '$posted',
          label: 'Awaiting Match',
          footer: posted > 0 ? 'Pending AI match' : 'None pending',
          footerColor: AppColors.inkMuted,
          onTap: () => context.go('/loads'),
        ),
        DashboardKpiCard(
          icon: Icons.handshake_outlined,
          iconBg: AppColors.statusMatchedBg,
          iconFg: AppColors.statusMatchedFg,
          value: '$matched',
          label: 'Matched',
          footer: matched > 0 ? 'Carrier assigned' : 'No matches yet',
          footerColor: AppColors.statusMatchedFg,
          onTap: () => context.go('/loads'),
        ),
        DashboardKpiCard(
          icon: Icons.navigation_outlined,
          iconBg: AppColors.statusInTransitBg,
          iconFg: AppColors.statusInTransitFg,
          value: '$inTransit',
          label: 'In Transit',
          footer: inTransit > 0 ? '$inTransit active now' : 'Nothing moving',
          footerColor: inTransit > 0 ? AppColors.statusInTransitFg : AppColors.inkMuted,
          onTap: () => context.go('/loads'),
        ),
        DashboardKpiCard(
          icon: Icons.check_circle_outline,
          iconBg: AppColors.statusSuccessBg,
          iconFg: AppColors.statusSuccessFg,
          value: '$delivered',
          label: 'Delivered',
          footer: 'All-time completed',
          footerColor: AppColors.statusSuccessFg,
          onTap: () => context.go('/loads'),
        ),
      ],
    );
  }
}

/// Clean placeholder skeleton rendered during initial stats fetch.
class _DashboardSkeleton extends StatelessWidget {
  const _DashboardSkeleton();

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      physics: const NeverScrollableScrollPhysics(),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 96,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              border: Border.all(color: AppColors.border),
            ),
          ),
          const SizedBox(height: AppConstants.spaceXl),
          GridView.count(
            crossAxisCount: 2,
            crossAxisSpacing: AppConstants.spaceMd,
            mainAxisSpacing: AppConstants.spaceMd,
            childAspectRatio: 1.25,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            children: List.generate(
              4,
              (index) => Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  border: Border.all(color: AppColors.border),
                ),
              ),
            ),
          ),
          const SizedBox(height: AppConstants.spaceXl),
          ...List.generate(
            3,
            (index) => Padding(
              padding: const EdgeInsets.only(bottom: AppConstants.spaceSm),
              child: Container(
                height: 64,
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  border: Border.all(color: AppColors.border),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
