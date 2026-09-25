import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class FleetListScreen extends StatelessWidget {
  const FleetListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Fleet Management'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/'),
        ),
      ),
      body: const Center(
        child: Text('List of vehicles goes here.'),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => context.go('/fleet/add'),
        child: const Icon(Icons.add),
      ),
    );
  }
}
