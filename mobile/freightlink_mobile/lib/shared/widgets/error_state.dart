import 'package:flutter/material.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_colors.dart';
import 'primary_button.dart';

/// Generic "something went wrong" state with a Retry action, matching the
/// "My Loads — Error State" mockup. [errorCode], if given, is rendered in a
/// small monospace box the way the mockup shows `ERR_CONNECTION_TIMEOUT`.
class ErrorState extends StatelessWidget {
  const ErrorState({
    super.key,
    required this.title,
    required this.message,
    required this.onRetry,
    this.errorCode,
  });

  final String title;
  final String message;
  final VoidCallback onRetry;
  final String? errorCode;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppConstants.spaceXl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: AppColors.statusErrorBg,
                borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              ),
              child: const Icon(
                Icons.cloud_off_rounded,
                size: 32,
                color: AppColors.statusErrorFg,
              ),
            ),
            const SizedBox(height: AppConstants.spaceLg),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: AppColors.ink,
              ),
            ),
            const SizedBox(height: AppConstants.spaceSm),
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 14, color: AppColors.inkMuted),
            ),
            const SizedBox(height: AppConstants.spaceLg),
            SizedBox(
              width: 180,
              child: PrimaryButton(
                label: 'Retry',
                icon: Icons.refresh_rounded,
                onPressed: onRetry,
              ),
            ),
            if (errorCode != null) ...[
              const SizedBox(height: AppConstants.spaceLg),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(AppConstants.spaceMd),
                decoration: BoxDecoration(
                  color: AppColors.statusNeutralBg,
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: Text(
                  errorCode!,
                  style: const TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 12,
                    color: AppColors.inkMuted,
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
