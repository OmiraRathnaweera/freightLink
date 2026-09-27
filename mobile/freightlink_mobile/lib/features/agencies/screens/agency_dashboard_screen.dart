import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/app_top_bar.dart';
import '../../../shared/widgets/auto_resize_text.dart';
import '../../../shared/widgets/dashboard_action_tile.dart';
import '../../../shared/widgets/dashboard_kpi_card.dart';
import '../../../shared/widgets/error_state.dart';
import '../../../shared/widgets/status_pill.dart';
import '../../auth/providers/auth_provider.dart';
import '../data/agencies_repository.dart';
import '../providers/agency_dashboard_provider.dart';

/// Redesigned Agency Dashboard screen providing an operational hub for
/// fleet dispatchers: real-time KPIs, fleet availability breakdown, and
/// categorized management actions.
class AgencyDashboardScreen extends StatelessWidget {
  const AgencyDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => AgencyDashboardProvider(context.read<AgenciesRepository>())..loadStats(),
      child: const _AgencyDashboardView(),
    );
  }
}

class _AgencyDashboardView extends StatelessWidget {
  const _AgencyDashboardView();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<AgencyDashboardProvider>();

    return Scaffold(
      appBar: const AppTopBar(
        title: 'Agency Dashboard',
        subtitle: 'Fleet Operations & Dispatch',
        leading: AppAvatar(),
        actions: [NotificationBellButton()],
      ),
      body: Builder(
        builder: (context) {
          if (provider.state == DashboardState.loading) {
            return const _DashboardSkeleton();
          }

          if (provider.state == DashboardState.error) {
            return Center(
              child: ErrorState(
                title: 'Unable to load dashboard',
                message: provider.errorMessage ?? 'Please check your connection and try again.',
                onRetry: provider.loadStats,
              ),
            );
          }

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
                  _AgencyHeroBanner(
                    profile: provider.agencyProfile,
                  ),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Operational Overview',
                    icon: Icons.analytics_outlined,
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  _KpiGrid(stats: provider.stats ?? {}),
                  const SizedBox(height: AppConstants.spaceLg),
                  _FleetUtilizationCard(stats: provider.stats ?? {}),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Dispatch & Operations',
                    icon: Icons.flash_on_outlined,
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  DashboardActionTile(
                    title: 'Fleet Management',
                    subtitle: 'Manage vehicle availability, capacity & specs',
                    icon: Icons.local_shipping_outlined,
                    badgeText: '${provider.stats?['totalVehicles'] ?? 0} Vehicles',
                    onTap: () => context.go('/dashboard/fleet'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Job Proposals',
                    subtitle: 'Review posted shipments and place bids',
                    icon: Icons.assignment_outlined,
                    badgeText: 'Open Bids',
                    onTap: () => context.go('/loads'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Agency Trips',
                    subtitle: 'Monitor active shipments, pickup & delivery proof',
                    icon: Icons.route_outlined,
                    badgeText: (provider.stats?['onTripVehicles'] ?? 0) > 0
                        ? '${provider.stats!['onTripVehicles']} In Transit'
                        : null,
                    isHighlight: (provider.stats?['onTripVehicles'] ?? 0) > 0,
                    onTap: () => context.push('/trips'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Billing & Invoices',
                    subtitle: 'Review invoices, payment receipts & settlements',
                    icon: Icons.receipt_long_outlined,
                    badgeText: 'Finance',
                    onTap: () => context.go('/payments'),
                  ),
                  const SizedBox(height: AppConstants.spaceXl),
                  _buildSectionHeader(
                    title: 'Administration & Compliance',
                    icon: Icons.admin_panel_settings_outlined,
                  ),
                  const SizedBox(height: AppConstants.spaceMd),
                  DashboardActionTile(
                    title: 'Driver Onboarding',
                    subtitle: 'Manage driver roster, status & credentials',
                    icon: Icons.person_add_outlined,
                    badgeText: '${provider.stats?['activeDrivers'] ?? 0} Drivers',
                    onTap: () => context.go('/dashboard/driver-onboarding'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Compliance Documents',
                    subtitle: 'Carrier permits, vehicle insurance & KYC records',
                    icon: Icons.description_outlined,
                    badgeText: (provider.stats?['pendingCompliance'] ?? 0) > 0
                        ? '${provider.stats!['pendingCompliance']} Action Needed'
                        : 'Verified',
                    isWarning: (provider.stats?['pendingCompliance'] ?? 0) > 0,
                    onTap: () => context.go('/dashboard/compliance-docs'),
                  ),
                  const SizedBox(height: AppConstants.spaceSm),
                  DashboardActionTile(
                    title: 'Agency Profile',
                    subtitle: 'Yard coordinates, registration & contact info',
                    icon: Icons.business_outlined,
                    onTap: () => context.go('/dashboard/profile'),
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

/// Hero card highlighting agency identity, staff details, and operational status.
class _AgencyHeroBanner extends StatelessWidget {
  const _AgencyHeroBanner({this.profile});

  final Map<String, dynamic>? profile;

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final agencyName = profile?['name'] as String? ?? 'Agency Fleet Hub';
    final yardAddress = profile?['yardAddress'] as String? ?? 'Primary Operations Yard';
    final staffName = user?.fullName.isNotEmpty == true ? user!.fullName : 'Agency Dispatcher';
    final status = profile?['status'] as String? ?? 'Active';

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
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
                  Icons.business_rounded,
                  color: AppColors.onPrimary,
                  size: 24,
                ),
              ),
              const SizedBox(width: AppConstants.spaceMd),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        const Expanded(
                          child: Text(
                            'DISPATCH HUB',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                              letterSpacing: 0.5,
                              color: AppColors.inkMuted,
                            ),
                          ),
                        ),
                        StatusPill(
                          label: status.toUpperCase(),
                          foreground: status.toLowerCase() == 'active'
                              ? AppColors.statusSuccessFg
                              : AppColors.statusInTransitFg,
                          background: status.toLowerCase() == 'active'
                              ? AppColors.statusSuccessBg
                              : AppColors.statusInTransitBg,
                        ),
                      ],
                    ),
                    const SizedBox(height: 2),
                    AutoResizeText(
                      agencyName,
                      maxLines: 1,
                      minFontSize: 13,
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        const Icon(
                          Icons.location_on_outlined,
                          size: 13,
                          color: AppColors.inkMuted,
                        ),
                        const SizedBox(width: 4),
                        Expanded(
                          child: AutoResizeText(
                            yardAddress,
                            maxLines: 1,
                            minFontSize: 10,
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.inkMuted,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),
          const Divider(height: 1),
          const SizedBox(height: AppConstants.spaceSm),
          Row(
            children: [
              const Icon(
                Icons.account_circle_outlined,
                size: 15,
                color: AppColors.inkMuted,
              ),
              const SizedBox(width: 6),
              Expanded(
                child: AutoResizeText(
                  'Signed in as $staffName',
                  maxLines: 1,
                  minFontSize: 10,
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w500,
                    color: AppColors.inkMuted,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              const Text(
                'Staff Portal',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: AppColors.inkFaint,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// 2x2 Grid of Key Performance Indicator cards.
class _KpiGrid extends StatelessWidget {
  const _KpiGrid({required this.stats});

  final Map<String, dynamic> stats;

  @override
  Widget build(BuildContext context) {
    final totalVehicles = stats['totalVehicles'] as int? ?? 0;
    final availableVehicles = stats['availableVehicles'] as int? ?? 0;
    final activeDrivers = stats['activeDrivers'] as int? ?? 0;
    final onTripVehicles = stats['onTripVehicles'] as int? ?? 0;
    final pendingCompliance = stats['pendingCompliance'] as int? ?? 0;

    return GridView.count(
      crossAxisCount: 2,
      crossAxisSpacing: AppConstants.spaceMd,
      mainAxisSpacing: AppConstants.spaceMd,
      childAspectRatio: 1.25,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      children: [
        DashboardKpiCard(
          icon: Icons.local_shipping_outlined,
          iconBg: AppColors.statusMatchedBg,
          iconFg: AppColors.statusMatchedFg,
          value: '$totalVehicles',
          label: 'Total Fleet',
          footer: '$availableVehicles available',
          footerColor: AppColors.statusSuccessFg,
          onTap: () => context.go('/dashboard/fleet'),
        ),
        DashboardKpiCard(
          icon: Icons.badge_outlined,
          iconBg: AppColors.statusNeutralBg,
          iconFg: AppColors.ink,
          value: '$activeDrivers',
          label: 'Active Drivers',
          footer: 'Roster ready',
          footerColor: AppColors.inkMuted,
          onTap: () => context.go('/dashboard/driver-onboarding'),
        ),
        DashboardKpiCard(
          icon: Icons.navigation_outlined,
          iconBg: AppColors.statusInTransitBg,
          iconFg: AppColors.statusInTransitFg,
          value: '$onTripVehicles',
          label: 'On Trip',
          footer: onTripVehicles > 0 ? '$onTripVehicles active now' : 'No trips in transit',
          footerColor: onTripVehicles > 0 ? AppColors.statusInTransitFg : AppColors.inkMuted,
          onTap: () => context.push('/trips'),
        ),
        DashboardKpiCard(
          icon: Icons.verified_user_outlined,
          iconBg: pendingCompliance > 0 ? AppColors.statusErrorBg : AppColors.statusSuccessBg,
          iconFg: pendingCompliance > 0 ? AppColors.statusErrorFg : AppColors.statusSuccessFg,
          value: '$pendingCompliance',
          label: 'Pending KYC',
          footer: pendingCompliance > 0 ? 'Action required' : 'All compliant',
          footerColor: pendingCompliance > 0 ? AppColors.statusErrorFg : AppColors.statusSuccessFg,
          onTap: () => context.go('/dashboard/compliance-docs'),
        ),
      ],
    );
  }
}

/// Visual breakdown card representing vehicle availability distribution.
class _FleetUtilizationCard extends StatelessWidget {
  const _FleetUtilizationCard({required this.stats});

  final Map<String, dynamic> stats;

  @override
  Widget build(BuildContext context) {
    final total = stats['totalVehicles'] as int? ?? 0;
    final available = stats['availableVehicles'] as int? ?? 0;
    final onTrip = stats['onTripVehicles'] as int? ?? 0;
    final maintenance = stats['maintenanceVehicles'] as int? ?? 0;

    final availabilityRate = total > 0 ? ((available / total) * 100).round() : 0;

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Row(
                children: [
                  Icon(
                    Icons.pie_chart_outline_rounded,
                    size: 16,
                    color: AppColors.inkMuted,
                  ),
                  SizedBox(width: AppConstants.spaceSm),
                  Text(
                    'FLEET AVAILABILITY',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.4,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ],
              ),
              StatusPill(
                label: '$availabilityRate% READY',
                foreground: AppColors.statusSuccessFg,
                background: AppColors.statusSuccessBg,
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spaceMd),
          ClipRRect(
            borderRadius: BorderRadius.circular(6),
            child: SizedBox(
              height: 10,
              child: total == 0
                  ? Container(color: AppColors.statusNeutralBg)
                  : Row(
                      children: [
                        if (available > 0)
                          Expanded(
                            flex: available,
                            child: Container(color: AppColors.statusSuccessFg),
                          ),
                        if (onTrip > 0)
                          Expanded(
                            flex: onTrip,
                            child: Container(color: AppColors.statusInTransitFg),
                          ),
                        if (maintenance > 0)
                          Expanded(
                            flex: maintenance,
                            child: Container(color: AppColors.inkFaint),
                          ),
                      ],
                    ),
            ),
          ),
          const SizedBox(height: AppConstants.spaceMd),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              _buildLegendDot(
                color: AppColors.statusSuccessFg,
                label: 'Available: $available',
              ),
              _buildLegendDot(
                color: AppColors.statusInTransitFg,
                label: 'On Trip: $onTrip',
              ),
              _buildLegendDot(
                color: AppColors.inkFaint,
                label: 'Maint: $maintenance',
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildLegendDot({required Color color, required String label}) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: color,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: 6),
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w500,
            color: AppColors.inkMuted,
          ),
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
            height: 104,
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
          const SizedBox(height: AppConstants.spaceLg),
          Container(
            height: 88,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              border: Border.all(color: AppColors.border),
            ),
          ),
          const SizedBox(height: AppConstants.spaceXl),
          ...List.generate(
            4,
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
