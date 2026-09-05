import 'package:flutter/material.dart';

import '../../../shared/widgets/status_pill.dart';
import '../models/load_status.dart';

/// [StatusPill] pre-colored for a [LoadStatus].
class LoadStatusBadge extends StatelessWidget {
  const LoadStatusBadge({super.key, required this.status});

  final LoadStatus status;

  @override
  Widget build(BuildContext context) {
    return StatusPill(
      label: status.label,
      foreground: status.foreground,
      background: status.background,
    );
  }
}
