import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../data/agencies_repository.dart';
import '../providers/drivers_provider.dart';
import '../../../shared/widgets/app_top_bar.dart';

class DriverListScreen extends StatelessWidget {
  const DriverListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => DriversProvider(context.read<AgenciesRepository>())..load(),
      child: Scaffold(
        appBar: AppTopBar(
          title: 'Driver Onboarding',
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: Consumer<DriversProvider>(
          builder: (context, provider, child) {
            if (provider.state == DriversState.loading) {
              return const Center(child: CircularProgressIndicator());
            }
            if (provider.state == DriversState.empty) {
              return const Center(child: Text('No drivers found.'));
            }
            if (provider.state == DriversState.error) {
              return Center(child: Text(provider.errorMessage ?? 'Error'));
            }
            return ListView.builder(
              itemCount: provider.drivers.length,
              itemBuilder: (context, index) {
                final driver = provider.drivers[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.person)),
                  title: Text(driver['fullName'] ?? 'Unknown Driver'),
                  subtitle: Text('License: ${driver['licenceNo'] ?? ''}'),
                  trailing: Text(driver['status'] ?? ''),
                );
              },
            );
          },
        ),
        floatingActionButton: FloatingActionButton(
          onPressed: () => context.go('/dashboard/driver-onboarding/add'),
          child: const Icon(Icons.add),
        ),
      ),
    );
  }
}
