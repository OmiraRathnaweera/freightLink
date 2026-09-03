import 'package:flutter/material.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';

/// The search field + filter button row from the My Loads / All Loads
/// mockups ("Search Load ID, Route..." + a tune icon).
class LoadSearchBar extends StatelessWidget {
  const LoadSearchBar({
    super.key,
    required this.onChanged,
    this.onFilterTap,
    this.hintText = 'Search load ID, origin, destination...',
  });

  final ValueChanged<String> onChanged;
  final VoidCallback? onFilterTap;
  final String hintText;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: TextField(
            onChanged: onChanged,
            decoration: InputDecoration(
              hintText: hintText,
              prefixIcon: const Icon(
                Icons.search_rounded,
                color: AppColors.inkMuted,
              ),
            ),
          ),
        ),
        const SizedBox(width: AppConstants.spaceSm),
        Container(
          decoration: BoxDecoration(
            border: Border.all(color: AppColors.border),
            borderRadius: BorderRadius.circular(10),
          ),
          child: IconButton(
            icon: const Icon(Icons.tune_rounded),
            onPressed: onFilterTap,
          ),
        ),
      ],
    );
  }
}
