import 'package:flutter/material.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../models/load_status.dart';

/// A horizontally scrollable row of status filter chips ("All", "Draft",
/// "Posted", …) shown directly below the search bar on My Loads / All
/// Loads. Tapping a chip calls [onChanged], which callers wire straight to
/// `LoadsListProvider.setStatusFilter` — a real `GET /loads?status=...`
/// call against the backend, not a client-side filter.
class LoadStatusFilterChips extends StatelessWidget {
  const LoadStatusFilterChips({
    super.key,
    required this.selected,
    required this.onChanged,
  });

  final LoadStatus? selected;
  final ValueChanged<LoadStatus?> onChanged;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          _FilterChip(
            label: 'All',
            isSelected: selected == null,
            foreground: AppColors.onPrimary,
            selectedBackground: AppColors.primary,
            onTap: () => onChanged(null),
          ),
          for (final status in LoadStatus.values) ...[
            const SizedBox(width: AppConstants.spaceSm),
            _FilterChip(
              label: status.label,
              isSelected: selected == status,
              foreground: status.foreground,
              selectedBackground: status.background,
              onTap: () => onChanged(status),
            ),
          ],
        ],
      ),
    );
  }
}

class _FilterChip extends StatelessWidget {
  const _FilterChip({
    required this.label,
    required this.isSelected,
    required this.foreground,
    required this.selectedBackground,
    required this.onTap,
  });

  final String label;
  final bool isSelected;
  final Color foreground;
  final Color selectedBackground;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(20),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected ? selectedBackground : AppColors.surface,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(
            color: isSelected ? selectedBackground : AppColors.border,
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: isSelected ? foreground : AppColors.inkMuted,
          ),
        ),
      ),
    );
  }
}
