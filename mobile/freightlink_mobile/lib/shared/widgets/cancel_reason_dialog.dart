import 'package:flutter/material.dart';

/// The "Cancel this load?" reason prompt, shared by Edit Load's trash icon
/// and Load Detail's "Cancel Load" action.
///
/// This owns its [TextEditingController] as `State` so it is disposed in
/// `dispose()`, once the widget's element actually unmounts — not manually
/// by the caller right after `showDialog` returns. Disposing it eagerly at
/// the call site raced the dialog's pop transition (worse with the
/// `TextField`'s `autofocus`, which was requesting focus on the same frame
/// the barrel/route was tearing down) and could hit a framework assertion
/// during unmount, so this widget avoids autofocus entirely and lets
/// `showDialog`'s own route disposal drive the controller's lifetime.
///
/// Returns the trimmed reason string via `Navigator.pop`, or `null` if the
/// user backed out. An empty reason is rejected inline rather than being
/// handed back for the caller to silently ignore.
Future<String?> showCancelReasonDialog(BuildContext context) {
  return showDialog<String>(
    context: context,
    builder: (_) => const CancelReasonDialog(),
  );
}

class CancelReasonDialog extends StatefulWidget {
  const CancelReasonDialog({super.key});

  @override
  State<CancelReasonDialog> createState() => _CancelReasonDialogState();
}

class _CancelReasonDialogState extends State<CancelReasonDialog> {
  final _controller = TextEditingController();
  String? _errorText;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _submit() {
    final reason = _controller.text.trim();
    if (reason.isEmpty) {
      setState(() => _errorText = 'A reason is required to cancel this load');
      return;
    }
    Navigator.of(context).pop(reason);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Cancel this load?'),
      content: TextField(
        controller: _controller,
        decoration: InputDecoration(
          labelText: 'Reason',
          hintText: 'Why is this load being cancelled?',
          errorText: _errorText,
        ),
        onChanged: (_) {
          if (_errorText != null) setState(() => _errorText = null);
        },
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Keep load'),
        ),
        TextButton(onPressed: _submit, child: const Text('Cancel load')),
      ],
    );
  }
}
