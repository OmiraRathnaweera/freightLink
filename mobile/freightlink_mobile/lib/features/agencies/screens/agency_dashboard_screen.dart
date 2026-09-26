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
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        'Total Vehicles: ${provider.stats?['totalVehicles'] ?? 0}',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      Text(
                        'Total Drivers: ${provider.stats?['activeDrivers'] ?? 0}',
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
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
