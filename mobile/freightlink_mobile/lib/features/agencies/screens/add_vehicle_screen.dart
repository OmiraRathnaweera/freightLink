import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class AddVehicleScreen extends StatelessWidget {
  const AddVehicleScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Add Vehicle'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/fleet'),
        ),
      ),
      body: const Center(
        child: Text('Add vehicle form goes here.'),
      ),
    );
  }
}
