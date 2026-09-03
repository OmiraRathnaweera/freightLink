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
    this.isFilterActive = false,
    this.hintText = 'Search load ID, origin, destination...',
  });

  final ValueChanged<String> onChanged;
  final VoidCallback? onFilterTap;

  /// Highlights the filter button when a status filter is currently applied,
  /// so it's visible at a glance without opening the filter sheet.
  final bool isFilterActive;

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
            color: isFilterActive ? AppColors.primary : null,
            border: Border.all(
              color: isFilterActive ? AppColors.primary : AppColors.border,
            ),
            borderRadius: BorderRadius.circular(10),
          ),
          child: IconButton(
            icon: Icon(
              Icons.tune_rounded,
              color: isFilterActive ? AppColors.onPrimary : AppColors.ink,
            ),
            onPressed: onFilterTap,
          ),
        ),
      ],
    );
  }
}
