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
import '../data/trips_repository.dart';
import '../providers/driver_dashboard_provider.dart';

/// Driver Dashboard screen — the Driver counterpart to [AgencyDashboardScreen]
/// and `ShipperDashboardScreen`: a hero banner and trip-pipeline KPIs (most
/// notably completed rides), drawn from [DriverDashboardProvider]'s
/// per-status trip counts. Completed / Cancelled / Total open the Trip History screen with the
/// matching filter; Active Trip goes to My Trip, the live screen where the driver acts on the trip.
class DriverDashboardScreen extends StatelessWidget {
  const DriverDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => DriverDashboardProvider(context.read<TripsRepository>())..loadStats(),
      child: const _DriverDashboardView(),
    );
  }
}

class _DriverDashboardView extends StatelessWidget {
  const _DriverDashboardView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<DriverDashboardProvider>();

    return Scaffold(
      appBar: const AppTopBar(
        title: 'Driver Dashboard',
        subtitle: 'Your Trips & Performance',
        leading: AppAvatar(),
        actions: [NotificationBellButton()],
      ),
      body: Builder(
        builder: (context) {
          if (provider.state == DriverDashboardState.loading) {
            return const _DashboardSkeleton();
          }

          if (provider.state == DriverDashboardState.error) {
            return Center(
              child: ErrorState(
                title: 'Unable to load dashboard',
                message: provider.errorMessage ?? 'Please check your connection and try again.',
                onRetry: provider.loadStats,
              ),
            );
          }

          final counts = provider.tripCounts;

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
                  const _DriverHeroBanner(),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Trip Performance',
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
                    title: 'My Trip',
                    subtitle: 'View your current pickup & delivery progress',
                    icon: Icons.local_shipping_outlined,
                    badgeText: (counts['assigned'] ?? 0) + (counts['pickedUp'] ?? 0) + (counts['inTransit'] ?? 0) > 0
                        ? 'Active'
                        : null,
                    isHighlight: true,
                    onTap: () => context.go('/loads'),
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  DashboardActionTile(
                    title: 'Trip History',
                    subtitle: 'Browse your completed, cancelled and past trips',
                    icon: Icons.history_rounded,
                    onTap: () => context.push('/dashboard/trip-history'),
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

/// Hero card greeting the signed-in Driver — the Driver counterpart to
/// Agency Dashboard's `_AgencyHeroBanner`.
class _DriverHeroBanner extends StatelessWidget {
  const _DriverHeroBanner();

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final driverName = user?.fullName.isNotEmpty == true ? user!.fullName : 'Driver';
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
              Icons.local_shipping_rounded,
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
                  'DRIVER PORTAL',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.5,
                    color: AppColors.inkMuted,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  driverName,
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

/// 2x2 Grid of trip-pipeline KPI cards, leading with completed rides.
class _KpiGrid extends StatelessWidget {
  const _KpiGrid({required this.counts});

  final Map<String, int> counts;

  @override
  Widget build(BuildContext context) {
    final delivered = counts['delivered'] ?? 0;
    final active = (counts['assigned'] ?? 0) + (counts['pickedUp'] ?? 0) + (counts['inTransit'] ?? 0);
    final cancelled = counts['cancelled'] ?? 0;
    final total = counts['total'] ?? 0;

    return GridView.count(
      crossAxisCount: 2,
      crossAxisSpacing: AppConstants.spaceMd,
      mainAxisSpacing: AppConstants.spaceMd,
      childAspectRatio: 1.25,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      children: [
        DashboardKpiCard(
          icon: Icons.check_circle_outline,
          iconBg: AppColors.statusSuccessBg,
          iconFg: AppColors.statusSuccessFg,
          value: '$delivered',
          label: 'Rides Completed',
          footer: 'All-time deliveries',
          footerColor: AppColors.statusSuccessFg,
          onTap: () => context.push('/dashboard/trip-history?filter=completed'),
        ),
        DashboardKpiCard(
          icon: Icons.navigation_outlined,
          iconBg: AppColors.statusInTransitBg,
          iconFg: AppColors.statusInTransitFg,
          value: '$active',
          label: 'Active Trip',
          footer: active > 0 ? 'In progress now' : 'None right now',
          footerColor: active > 0 ? AppColors.statusInTransitFg : AppColors.inkMuted,
          onTap: () => context.go('/loads'),
        ),
        DashboardKpiCard(
          icon: Icons.cancel_outlined,
          iconBg: cancelled > 0 ? AppColors.statusErrorBg : AppColors.statusNeutralBg,
          iconFg: cancelled > 0 ? AppColors.statusErrorFg : AppColors.ink,
          value: '$cancelled',
          label: 'Cancelled',
          footer: cancelled > 0 ? 'Review history' : 'None cancelled',
          footerColor: cancelled > 0 ? AppColors.statusErrorFg : AppColors.inkMuted,
          onTap: () => context.push('/dashboard/trip-history?filter=cancelled'),
        ),
        DashboardKpiCard(
          icon: Icons.local_shipping_outlined,
          iconBg: AppColors.statusMatchedBg,
          iconFg: AppColors.statusMatchedFg,
          value: '$total',
          label: 'Total Trips',
          footer: 'Since joining',
          footerColor: AppColors.statusMatchedFg,
          onTap: () => context.push('/dashboard/trip-history'),
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
          Container(
            height: 64,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              border: Border.all(color: AppColors.border),
            ),
          ),
        ],
      ),
    );
  }
}
