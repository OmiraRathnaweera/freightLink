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
                return ListTile(
                  title: Text('Vehicle ${index + 1}'),
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
