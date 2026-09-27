import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../data/billing_repository.dart';

class RaiseDisputeScreen extends StatefulWidget {
  const RaiseDisputeScreen({super.key, required this.tripId});

  final String tripId;

  @override
  State<RaiseDisputeScreen> createState() => _RaiseDisputeScreenState();
}

class _RaiseDisputeScreenState extends State<RaiseDisputeScreen> {
  final _formKey = GlobalKey<FormState>();
  final _description = TextEditingController();
  String _category = 'Damage';
  bool _submitting = false;

  @override
  void dispose() {
    _description.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    try {
      await context.read<BillingRepository>().raiseDispute(
            tripId: widget.tripId,
            category: _category,
            description: _description.text.trim(),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Dispute raised and sent for review.')),
      );
      context.pop();
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.toString())));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Raise a dispute')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('Trip ${widget.tripId}', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 20),
              DropdownButtonFormField<String>(
                value: _category,
                decoration: const InputDecoration(labelText: 'Category'),
                items: const [
                  DropdownMenuItem(value: 'Damage', child: Text('Damage')),
                  DropdownMenuItem(value: 'Delay', child: Text('Delay')),
                  DropdownMenuItem(value: 'Billing', child: Text('Billing')),
                  DropdownMenuItem(value: 'Other', child: Text('Other')),
                ],
                onChanged: _submitting ? null : (value) => setState(() => _category = value!),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _description,
                enabled: !_submitting,
                minLines: 5,
                maxLines: 8,
                decoration: const InputDecoration(labelText: 'What happened?', alignLabelWithHint: true),
                validator: (value) => value == null || value.trim().length < 10
                    ? 'Describe the issue in at least 10 characters.'
                    : null,
              ),
              const Spacer(),
              FilledButton(
                onPressed: _submitting ? null : _submit,
                child: Text(_submitting ? 'Submitting…' : 'Submit dispute'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
