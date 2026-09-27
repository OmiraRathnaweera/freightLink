import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../data/agencies_repository.dart';
import '../providers/agency_dashboard_provider.dart';
import '../../../shared/widgets/app_top_bar.dart';

class AgencyDashboardScreen extends StatelessWidget {
  const AgencyDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => AgencyDashboardProvider(context.read<AgenciesRepository>())..loadStats(),
      child: Scaffold(
        appBar: const AppTopBar(
          title: 'Agency Dashboard',
        ),
        body: Consumer<AgencyDashboardProvider>(
          builder: (context, provider, child) {
            if (provider.state == DashboardState.loading) {
              return const Center(child: CircularProgressIndicator());
            }
            if (provider.state == DashboardState.error) {
              return Center(child: Text(provider.errorMessage ?? 'Error'));
            }

            return Padding(
              padding: const EdgeInsets.all(16.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Wrap(
                    spacing: 10,
                    runSpacing: 10,
                    children: [
                      _MetricChip(label: 'Vehicles', value: provider.stats?['totalVehicles'] ?? 0, icon: Icons.local_shipping_outlined),
                      _MetricChip(label: 'Active drivers', value: provider.stats?['activeDrivers'] ?? 0, icon: Icons.person_outline),
                      _MetricChip(label: 'Available', value: provider.stats?['availableVehicles'] ?? 0, icon: Icons.check_circle_outline),
                      _MetricChip(label: 'Compliance pending', value: provider.stats?['pendingCompliance'] ?? 0, icon: Icons.assignment_late_outlined),
                    ],
                  ),
                  const SizedBox(height: 20),
                  Expanded(
                    child: GridView.count(
                      crossAxisCount: 2,
                      crossAxisSpacing: 16,
                      mainAxisSpacing: 16,
                      children: [
                        _DashboardCard(
                          title: 'Agency Profile',
                          icon: Icons.business,
                          onTap: () => context.go('/dashboard/profile'),
                        ),
                        _DashboardCard(
                          title: 'Fleet Management',
                          icon: Icons.local_shipping,
                          onTap: () => context.go('/dashboard/fleet'),
                        ),
                        _DashboardCard(
                          title: 'Driver Onboarding',
                          icon: Icons.person_add,
                          onTap: () => context.go('/dashboard/driver-onboarding'),
                        ),
                        _DashboardCard(
                          title: 'Compliance Documents',
                          icon: Icons.description,
                          onTap: () => context.go('/dashboard/compliance-docs'),
                        ),
                        _DashboardCard(
                          title: 'Job Proposals',
                          icon: Icons.assignment_outlined,
                          onTap: () => context.go('/loads'),
                        ),
                        _DashboardCard(
                          title: 'Agency Trips',
                          icon: Icons.route_outlined,
                          onTap: () => context.push('/trips'),
                        ),
                        _DashboardCard(
                          title: 'Billing',
                          icon: Icons.receipt_long_outlined,
                          onTap: () => context.go('/payments'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class _MetricChip extends StatelessWidget {
  const _MetricChip({required this.label, required this.value, required this.icon});

  final String label;
  final Object value;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Chip(
        avatar: Icon(icon, size: 18),
        label: Text('$label: $value'),
      );
}

class _DashboardCard extends StatelessWidget {
  const _DashboardCard({
    required this.title,
    required this.icon,
    required this.onTap,
  });

  final String title;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 48, color: Theme.of(context).primaryColor),
              const SizedBox(height: 12),
              Text(
                title,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
