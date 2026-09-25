import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../data/agencies_repository.dart';
import '../providers/driver_onboarding_provider.dart';

class DriverOnboardingScreen extends StatelessWidget {
  const DriverOnboardingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => DriverOnboardingProvider(context.read<AgenciesRepository>()),
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Driver Onboarding'),
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: Consumer<DriverOnboardingProvider>(
          builder: (context, provider, child) {
            if (provider.isSubmitting) {
              return const Center(child: CircularProgressIndicator());
            }
            if (provider.isSuccess) {
              return const Center(child: Text('Driver registered successfully!'));
            }
            return Center(
              child: ElevatedButton(
                onPressed: () => provider.submitDriver({}),
                child: const Text('Submit Dummy Driver'),
              ),
            );
          },
        ),
      ),
    );
  }
}
