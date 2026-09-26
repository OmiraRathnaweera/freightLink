import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../providers/notification_provider.dart';

/// Floating push-style notification banner rendered at the top of the app shell.
class InAppPushBanner extends StatelessWidget {
  const InAppPushBanner({super.key});

  @override
  Widget build(BuildContext context) {
    NotificationProvider? provider;
    try {
      provider = Provider.of<NotificationProvider>(context, listen: true);
    } catch (_) {
      provider = null;
    }
    final notification = provider?.currentPushBanner;

    return AnimatedPositioned(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeOutCubic,
      top: notification != null ? 12 : -150,
      left: AppConstants.spaceLg,
      right: AppConstants.spaceLg,
      child: IgnorePointer(
        ignoring: notification == null,
        child: SafeArea(
        child: Material(
          type: MaterialType.transparency,
          child: notification == null
              ? const SizedBox.shrink()
              : Container(
                  key: const Key('in_app_push_banner'),
                  padding: const EdgeInsets.symmetric(
                    horizontal: AppConstants.spaceLg,
                    vertical: AppConstants.spaceMd,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                    border: Border.all(
                      color: notification.iconColor.withValues(alpha: 0.3),
                      width: 1.5,
                    ),
                    boxShadow: const [
                      BoxShadow(
                        color: Color(0x1F000000),
                        blurRadius: 16,
                        offset: Offset(0, 8),
                      ),
                    ],
                  ),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: notification.iconBackgroundColor,
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          notification.icon,
                          color: notification.iconColor,
                          size: 22,
                        ),
                      ),
                      const SizedBox(width: AppConstants.spaceMd),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    notification.title,
                                    style: const TextStyle(
                                      fontSize: 14,
                                      fontWeight: FontWeight.w700,
                                      color: AppColors.ink,
                                      letterSpacing: -0.2,
                                    ),
                                  ),
                                ),
                                const Text(
                                  'Now',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: AppColors.inkMuted,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 2),
                            Text(
                              notification.message,
                              style: const TextStyle(
                                fontSize: 12,
                                color: AppColors.inkMuted,
                                height: 1.3,
                              ),
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: AppConstants.spaceSm),
                      IconButton(
                        key: const Key('push_banner_dismiss_button'),
                        icon: const Icon(Icons.close_rounded, size: 18),
                        color: AppColors.inkMuted,
                        onPressed: () {
                          context.read<NotificationProvider>().dismissPushBanner();
                        },
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(
                          minWidth: 28,
                          minHeight: 28,
                        ),
                      ),
                    ],
                  ),
                ),
        ),
      ),
    ),
  );
}
}
