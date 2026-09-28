import 'package:flutter/material.dart';

import '../../core/constants/app_constants.dart';
import '../../core/theme/app_colors.dart';
import 'auto_resize_text.dart';

/// A single Key Performance Indicator card used by role dashboards (Agency,
/// Shipper): an icon, a big value, a label, a footer line, and a forward
/// affordance that navigates somewhere more detailed on tap.
class DashboardKpiCard extends StatelessWidget {
  const DashboardKpiCard({
    super.key,
    required this.icon,
    required this.iconBg,
    required this.iconFg,
    required this.value,
    required this.label,
    required this.footer,
    required this.footerColor,
    required this.onTap,
  });

  final IconData icon;
  final Color iconBg;
  final Color iconFg;
  final String value;
  final String label;
  final String footer;
  final Color footerColor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(AppConstants.spaceMd),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Container(
                    width: 34,
                    height: 34,
                    decoration: BoxDecoration(
                      color: iconBg,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Icon(icon, size: 18, color: iconFg),
                  ),
                  const Icon(
                    Icons.arrow_forward_rounded,
                    size: 14,
                    color: AppColors.inkFaint,
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  AutoResizeText.kpi(
                    value,
                    minFontSize: 13,
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      color: AppColors.ink,
                      height: 1.1,
                    ),
                  ),
                  const SizedBox(height: 2),
                  AutoResizeText(
                    label,
                    maxLines: 1,
                    minFontSize: 10,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.inkMuted,
                    ),
                  ),
                ],
              ),
              AutoResizeText(
                footer,
                maxLines: 1,
                minFontSize: 9,
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: footerColor,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
