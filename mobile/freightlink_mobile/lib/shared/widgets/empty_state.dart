import 'package:flutter/material.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_colors.dart';
import 'primary_button.dart';

/// Generic "nothing here yet" state: icon in a rounded box, title, subtitle,
/// and an optional call-to-action button. Matches the "My Loads — Empty
/// State" mockup; reusable by any feature's empty list.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;
  final String title;
  final String? message;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppConstants.spaceXl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 96,
              height: 96,
              decoration: BoxDecoration(
                color: AppColors.statusNeutralBg,
                borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              ),
              child: Icon(icon, size: 40, color: AppColors.inkFaint),
            ),
            const SizedBox(height: AppConstants.spaceXl),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 20,
                fontWeight: FontWeight.w700,
                color: AppColors.ink,
              ),
            ),
            if (message != null) ...[
              const SizedBox(height: AppConstants.spaceSm),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 14, color: AppColors.inkMuted),
              ),
            ],
            if (actionLabel != null && onAction != null) ...[
              const SizedBox(height: AppConstants.spaceXl),
              SizedBox(
                width: 220,
                child: PrimaryButton(
                  label: actionLabel!,
                  icon: Icons.add,
                  onPressed: onAction,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
