import 'package:flutter/material.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_colors.dart';

/// The bordered white card with a small icon+label header, used for every
/// info block across the Loads screens (Route & Cargo, Documents, Activity
/// Log, form sections, …).
class SectionCard extends StatelessWidget {
  const SectionCard({
    super.key,
    this.title,
    this.icon,
    required this.child,
    this.trailing,
    this.padding,
  });

  final String? title;
  final IconData? icon;
  final Widget child;
  final Widget? trailing;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      padding: padding ?? const EdgeInsets.all(AppConstants.spaceLg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null) ...[
            Row(
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 16, color: AppColors.inkMuted),
                  const SizedBox(width: AppConstants.spaceSm),
                ],
                Expanded(
                  child: Text(
                    title!.toUpperCase(),
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      letterSpacing: 0.4,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ),
                ?trailing,
              ],
            ),
            const SizedBox(height: AppConstants.spaceMd),
          ],
          child,
        ],
      ),
    );
  }
}
