import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../providers/compliance_docs_provider.dart';
import '../data/agencies_repository.dart';
import '../../../shared/widgets/app_top_bar.dart';

class ComplianceDocsScreen extends StatelessWidget {
  const ComplianceDocsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => ComplianceDocsProvider(context.read<AgenciesRepository>())..loadDocs(),
      child: Scaffold(
        appBar: AppTopBar(
          title: 'Compliance Documents',
          leading: IconButton(
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/dashboard'),
          ),
        ),
        body: Consumer<ComplianceDocsProvider>(
          builder: (context, provider, child) {
            if (provider.state == ComplianceDocsState.loading) {
              return const Center(child: CircularProgressIndicator());
            }
            if (provider.state == ComplianceDocsState.error) {
              return Center(child: Text(provider.errorMessage ?? 'An error occurred'));
            }

            if (provider.docs.isEmpty) {
              return const Center(
                child: Text('No compliance documents found.\nTap + to add one.', textAlign: TextAlign.center),
              );
            }

            return ListView.builder(
              padding: const EdgeInsets.all(16.0),
              itemCount: provider.docs.length,
              itemBuilder: (context, index) {
                final doc = provider.docs[index];
                return Card(
                  elevation: 2,
                  margin: const EdgeInsets.only(bottom: 16.0),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  child: ListTile(
                    contentPadding: const EdgeInsets.all(16.0),
                    leading: const CircleAvatar(
                      backgroundColor: Colors.blueAccent,
                      child: Icon(Icons.description, color: Colors.white),
                    ),
                    title: Text(doc['docType'] ?? 'Unknown Type', style: const TextStyle(fontWeight: FontWeight.bold)),
                    // An admin-verified document is frozen — it can't be replaced or edited.
                    trailing: doc['status'] == 'Verified'
                        ? const Tooltip(message: 'Verified – locked', child: Icon(Icons.lock_outline, color: Colors.green))
                        : null,
                    subtitle: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const SizedBox(height: 4),
                        Text('Doc No: ${doc['docNumber'] ?? 'N/A'}'),
                        const SizedBox(height: 4),
                        Text('Status: ${doc['status'] ?? 'Pending'}', style: TextStyle(color: doc['status'] == 'Verified' ? Colors.green : (doc['status'] == 'Rejected' ? Colors.red : Colors.orange))),
                      ],
                    ),
                  ),
                );
              },
            );
          },
        ),
        floatingActionButton: Builder(
          builder: (context) => FloatingActionButton(
            onPressed: () {
              context.go('/dashboard/compliance-docs/add');
            },
            child: const Icon(Icons.add),
          ),
        ),
      ),
    );
  }
}
