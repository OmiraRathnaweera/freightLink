import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../shared/widgets/app_top_bar.dart';

class ComplianceDocsScreen extends StatelessWidget {
  const ComplianceDocsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(
        title: 'Compliance Documents',
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
