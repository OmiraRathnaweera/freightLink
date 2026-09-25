import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class ComplianceDocsScreen extends StatelessWidget {
  const ComplianceDocsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Compliance Documents'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () => context.go('/dashboard'),
        ),
      ),
      body: const Center(
        child: Text('Upload compliance documents interface goes here.'),
      ),
    );
  }
}
