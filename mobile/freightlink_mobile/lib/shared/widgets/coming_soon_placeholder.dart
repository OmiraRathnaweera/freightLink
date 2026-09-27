import 'package:flutter/material.dart';

import 'app_top_bar.dart';
import 'empty_state.dart';

/// Placeholder tab content for features not built yet (Dashboard, Payments)
/// — the bottom nav is real, these tabs' screens aren't.
class ComingSoonPlaceholder extends StatelessWidget {
  const ComingSoonPlaceholder({
    super.key,
    required this.title,
    required this.icon,
    this.bottomLeading,
    this.actions = const [],
  });

  final String title;
  final IconData icon;
  final Widget? bottomLeading;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppTopBar(title: title, leading: bottomLeading, actions: actions),
      body: EmptyState(
        icon: icon,
        title: 'Coming soon',
        message: '$title isn’t built yet.',
      ),
    );
  }
}
