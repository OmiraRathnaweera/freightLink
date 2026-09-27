import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../data/loads_repository.dart';

/// Shipper-only pre-match estimate and AI carrier recommendation review.
class LoadMatchScreen extends StatefulWidget {
  const LoadMatchScreen({super.key, required this.loadId});
  final String loadId;

  @override
  State<LoadMatchScreen> createState() => _LoadMatchScreenState();
}

class _LoadMatchScreenState extends State<LoadMatchScreen> {
  Map<String, dynamic>? _estimate;
  Map<String, dynamic>? _match;
  Object? _error;
  bool _loading = true;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repository = context.read<LoadsRepository>();
      final results = await Future.wait([
        repository.estimatePrice(widget.loadId),
        repository.getMatchRecommendation(widget.loadId),
      ]);
      if (!mounted) return;
      setState(() {
        _estimate = results[0];
        _match = results[1];
      });
    } catch (error) {
      if (mounted) setState(() => _error = error);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _decide(String action) async {
    final recommended = _match?['recommendedAgency'] as Map<String, dynamic>?;
    if (action == 'confirm' && recommended == null) return;
    String? reason;
    if (action != 'confirm') {
      reason = await _askReason(action == 'reject' ? 'Reject match' : 'Request revision');
      if (reason == null) return;
    }
    setState(() => _submitting = true);
    try {
      final repository = context.read<LoadsRepository>();
      if (action == 'confirm') {
        await repository.confirmMatch(widget.loadId, recommended!['agencyId'] as String);
      } else if (action == 'reject') {
        await repository.rejectMatch(widget.loadId, reason!);
      } else {
        await repository.reviseMatch(widget.loadId, reason!);
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Match decision recorded.')));
      await _load();
    } catch (error) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$error')));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  // Mirrors MatchDecisionRequestDto: [Required][StringLength(1000, MinimumLength = 5)]
  // (backend/DTOs/Loads/MatchDecisionRequestDto.cs).
  static const _reasonMinLength = 5;
  static const _reasonMaxLength = 1000;

  Future<String?> _askReason(String title) async {
    final controller = TextEditingController();
    String? errorText;
    return showDialog<String>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text(title),
          content: TextField(
            controller: controller,
            minLines: 3,
            maxLines: 5,
            maxLength: _reasonMaxLength,
            autofocus: true,
            decoration: InputDecoration(
              hintText: 'Explain your decision (at least $_reasonMinLength characters)',
              errorText: errorText,
            ),
            onChanged: (_) {
              if (errorText != null) setDialogState(() => errorText = null);
            },
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(dialogContext), child: const Text('Cancel')),
            FilledButton(
              onPressed: () {
                final value = controller.text.trim();
                if (value.length < _reasonMinLength) {
                  setDialogState(() => errorText = 'Enter at least $_reasonMinLength characters.');
                  return;
                }
                if (value.length > _reasonMaxLength) {
                  setDialogState(() => errorText = 'Must be $_reasonMaxLength characters or fewer.');
                  return;
                }
                Navigator.pop(dialogContext, value);
              },
              child: const Text('Submit'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final recommended = _match?['recommendedAgency'] as Map<String, dynamic>?;
    return Scaffold(
      appBar: AppBar(title: const Text('Estimate & AI Match')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: FilledButton(onPressed: _load, child: const Text('Retry')))
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    Card(
                      child: ListTile(
                        leading: const Icon(Icons.payments_outlined),
                        title: const Text('Your estimated price'),
                        subtitle: Text('LKR ${_estimate?['estimatedPrice'] ?? '—'} · ${_estimate?['distanceKm'] ?? '—'} km'),
                      ),
                    ),
                    const SizedBox(height: 12),
                    if (recommended == null)
                      const Card(child: Padding(padding: EdgeInsets.all(16), child: Text('No AI recommendation is available yet.')))
                    else ...[
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                            Text(recommended['name'] as String? ?? 'Recommended agency', style: Theme.of(context).textTheme.titleLarge),
                            const SizedBox(height: 8),
                            Text(recommended['yardAddress'] as String? ?? ''),
                            Text('Estimated match price: LKR ${recommended['estimatedPrice'] ?? '—'}'),
                            Text('Positioning ETA: ${recommended['positioningEtaMinutes'] ?? '—'} min'),
                            if ((recommended['selectionJustification'] as String?)?.isNotEmpty ?? false) ...[
                              const SizedBox(height: 8),
                              Text(recommended['selectionJustification'] as String),
                            ],
                          ]),
                        ),
                      ),
                      const SizedBox(height: 16),
                      FilledButton(onPressed: _submitting ? null : () => _decide('confirm'), child: const Text('Approve match')),
                      const SizedBox(height: 8),
                      OutlinedButton(onPressed: _submitting ? null : () => _decide('revise'), child: const Text('Request revision')),
                      TextButton(onPressed: _submitting ? null : () => _decide('reject'), child: const Text('Reject match')),
                    ],
                  ],
                ),
    );
  }
}
