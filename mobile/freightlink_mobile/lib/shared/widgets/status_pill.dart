import 'package:flutter/material.dart';

/// A small colored pill/badge, e.g. for load statuses. Feature-level badges
/// (like `LoadStatusBadge`) pick the [foreground]/[background] and wrap this.
class StatusPill extends StatelessWidget {
  const StatusPill({
    super.key,
    required this.label,
    required this.foreground,
    required this.background,
  });

  final String label;
  final Color foreground;
  final Color background;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(6),
      ),
      child: FittedBox(
        fit: BoxFit.scaleDown,
        child: Text(
          label.toUpperCase(),
          maxLines: 1,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.3,
            color: foreground,
          ),
        ),
      ),
    );
  }
}
