import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../data/agencies_repository.dart';
import '../providers/fleet_provider.dart';
import '../../../shared/widgets/app_top_bar.dart';

class FleetListScreen extends StatelessWidget {
  const FleetListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => FleetProvider(context.read<AgenciesRepository>())..load(),
      child: Scaffold(
        appBar: AppTopBar(
          title: 'Fleet Management',
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: Consumer<FleetProvider>(
          builder: (context, provider, child) {
            if (provider.state == FleetState.loading) {
              return const Center(child: CircularProgressIndicator());
            }
            if (provider.state == FleetState.empty) {
              return const Center(child: Text('No vehicles found.'));
            }
            if (provider.state == FleetState.error) {
              return Center(child: Text(provider.errorMessage ?? 'Error'));
            }
            return ListView.builder(
              itemCount: provider.vehicles.length,
              itemBuilder: (context, index) {
                final vehicle = provider.vehicles[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.local_shipping)),
                  title: Text(vehicle['registrationNo'] ?? 'Unknown Vehicle'),
                  subtitle: Text('${vehicle['vehicleType'] ?? ''} - Status: ${vehicle['status'] ?? ''}'),
                  trailing: PopupMenuButton<String>(
                    tooltip: 'Manage availability',
                    onSelected: (status) async {
                      final success = await provider.updateVehicleStatus(
                        vehicle['vehicleId'] as String,
                        status,
                      );
                      if (!context.mounted) return;
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(success
                              ? 'Vehicle availability updated to $status.'
                              : provider.errorMessage ?? 'Could not update vehicle availability.'),
                        ),
                      );
                    },
                    itemBuilder: (context) => const [
                      PopupMenuItem(value: 'Available', child: Text('Mark available')),
                      PopupMenuItem(value: 'Maintenance', child: Text('Mark in maintenance')),
                      PopupMenuItem(value: 'Retired', child: Text('Retire vehicle')),
                    ],
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text('${vehicle['capacityKg'] ?? '0'} kg'),
                        const SizedBox(height: 2),
                        const Icon(Icons.more_vert, size: 18),
                      ],
                    ),
                  ),
                );
              },
            );
          },
        ),
        floatingActionButton: FloatingActionButton(
          onPressed: () => context.go('/dashboard/fleet/add'),
          child: const Icon(Icons.add),
        ),
      ),
    );
  }
}
