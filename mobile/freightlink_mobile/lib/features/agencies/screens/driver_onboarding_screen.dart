import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class DriverOnboardingScreen extends StatelessWidget {
  const DriverOnboardingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Driver Onboarding'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/'),
        ),
      ),
      body: const Center(
        child: Text('Driver onboarding form goes here.'),
      ),
    );
  }
}
