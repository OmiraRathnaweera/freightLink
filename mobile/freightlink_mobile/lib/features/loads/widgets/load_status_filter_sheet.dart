import 'package:flutter/material.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../models/load_status.dart';
import '../providers/loads_list_provider.dart';
import 'load_status_badge.dart';

/// Opens the status-filter bottom sheet (the tune icon on My Loads / All
/// Loads) and applies the chosen [LoadStatus] — or clears it — on
/// [provider].
///
/// [provider] is captured directly rather than looked up via
/// `Provider.of`/`context.watch` from inside the sheet: a
/// `showModalBottomSheet` route is inserted above the calling screen's own
/// widget tree, so it isn't a descendant of a `ChangeNotifierProvider`
/// scoped locally to that screen (as `LoadsListProvider` is here) — passing
/// the instance in directly sidesteps that lookup entirely.
Future<void> showLoadStatusFilterSheet(
  BuildContext context,
  LoadsListProvider provider,
) {
  return showModalBottomSheet<void>(
    context: context,
    backgroundColor: AppColors.surface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (sheetContext) => _LoadStatusFilterSheet(
      currentStatus: provider.statusFilter,
      onSelected: (status) {
        provider.setStatusFilter(status);
        Navigator.of(sheetContext).pop();
      },
    ),
  );
}

class _LoadStatusFilterSheet extends StatelessWidget {
  const _LoadStatusFilterSheet({
    required this.currentStatus,
    required this.onSelected,
  });

  final LoadStatus? currentStatus;
  final ValueChanged<LoadStatus?> onSelected;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
          AppConstants.spaceLg,
          AppConstants.spaceLg,
          AppConstants.spaceLg,
          AppConstants.spaceXl,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Filter by status',
                  style: TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                    color: AppColors.ink,
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.close_rounded),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spaceSm),
            _StatusOptionTile(
              label: 'All statuses',
              isSelected: currentStatus == null,
              onTap: () => onSelected(null),
            ),
            for (final status in LoadStatus.values)
              _StatusOptionTile(
                label: status.label,
                badge: LoadStatusBadge(status: status),
                isSelected: currentStatus == status,
                onTap: () => onSelected(status),
              ),
          ],
        ),
      ),
    );
  }
}

class _StatusOptionTile extends StatelessWidget {
  const _StatusOptionTile({
    required this.label,
    required this.isSelected,
    required this.onTap,
    this.badge,
  });

  final String label;
  final bool isSelected;
  final VoidCallback onTap;
  final Widget? badge;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          vertical: AppConstants.spaceMd,
          horizontal: AppConstants.spaceSm,
        ),
        child: Row(
          children: [
            Expanded(
              child:
                  badge ??
                  Text(
                    label,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: AppColors.ink,
                    ),
                  ),
            ),
            Icon(
              isSelected
                  ? Icons.radio_button_checked_rounded
                  : Icons.radio_button_unchecked_rounded,
              color: isSelected ? AppColors.primary : AppColors.inkFaint,
              size: 22,
            ),
          ],
        ),
      ),
    );
  }
}
