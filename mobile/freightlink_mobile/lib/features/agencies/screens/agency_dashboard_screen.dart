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
                  Text('Total Vehicles: ${provider.stats?['totalVehicles'] ?? 0}'),
                  const SizedBox(height: 20),
                  ElevatedButton(
                    onPressed: () => context.go('/dashboard/fleet'),
                    child: const Text('Fleet Management'),
                  ),
                  const SizedBox(height: 10),
                  ElevatedButton(
                    onPressed: () => context.go('/dashboard/driver-onboarding'),
                    child: const Text('Driver Onboarding'),
                  ),
                  const SizedBox(height: 10),
                  ElevatedButton(
                    onPressed: () => context.go('/dashboard/compliance-docs'),
                    child: const Text('Compliance Documents'),
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
