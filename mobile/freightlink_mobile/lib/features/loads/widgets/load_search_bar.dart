import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

/// The search field from the My Loads / All Loads mockups ("Search Load ID,
/// Route..."). Status filtering lives in [LoadStatusFilterChips], shown
/// directly below this.
class LoadSearchBar extends StatelessWidget {
  const LoadSearchBar({
    super.key,
    required this.onChanged,
    this.hintText = 'Search load ID, origin, destination...',
  });

  final ValueChanged<String> onChanged;
  final String hintText;

  @override
  Widget build(BuildContext context) {
    return TextField(
      onChanged: onChanged,
      decoration: InputDecoration(
        hintText: hintText,
        prefixIcon: const Icon(Icons.search_rounded, color: AppColors.inkMuted),
      ),
    );
  }
}
