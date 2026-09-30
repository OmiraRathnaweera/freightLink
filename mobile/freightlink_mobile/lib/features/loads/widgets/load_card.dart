import 'package:flutter/material.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../../../shared/widgets/auto_resize_text.dart';
import '../models/load.dart';
import '../models/load_status.dart';
import 'load_status_badge.dart';

/// The load list-item card used by both **My Loads** and the Admin
/// **All Loads** screen (`FM-7942 · Matched · Colombo Port → Kandy Yard`).
/// [showShipper] renders the Shipper name row for the admin list.
class LoadCard extends StatelessWidget {
  const LoadCard({
    super.key,
    required this.load,
    this.onTap,
    this.showShipper = false,
  });

  final LoadListItem load;
  final VoidCallback? onTap;
  final bool showShipper;

  @override
  Widget build(BuildContext context) {
    final isClosed =
        load.status == LoadStatus.delivered ||
        load.status == LoadStatus.closed ||
        load.status == LoadStatus.cancelled;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: Opacity(
        opacity: isClosed ? 0.6 : 1,
        child: Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(AppConstants.spaceLg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: AutoResizeText(
                      load.referenceCode,
                      maxLines: 1,
                      minFontSize: 12,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppColors.ink,
                        fontFeatures: [FontFeature.tabularFigures()],
                      ),
                    ),
                  ),
                  const SizedBox(width: AppConstants.spaceSm),
                  LoadStatusBadge(status: load.status),
                ],
              ),
              const SizedBox(height: AppConstants.spaceMd),
              Row(
                children: [
                  Expanded(
                    child: AutoResizeText(
                      load.pickupAddress,
                      maxLines: 1,
                      minFontSize: 11,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                    ),
                  ),
                  const Padding(
                    padding: EdgeInsets.symmetric(
                      horizontal: AppConstants.spaceSm,
                    ),
                    child: Icon(
                      Icons.arrow_forward_rounded,
                      size: 16,
                      color: AppColors.inkFaint,
                    ),
                  ),
                  Expanded(
                    child: AutoResizeText(
                      load.dropoffAddress,
                      maxLines: 1,
                      minFontSize: 11,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppColors.ink,
                      ),
                      textAlign: TextAlign.end,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spaceMd),
              if (showShipper) ...[
                _KeyValueRow(label: 'Shipper', value: load.shipperName),
                const SizedBox(height: 4),
              ],
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Flexible(
                    child: AutoResizeText(
                      AppFormatters.weightKg(load.weightKg),
                      maxLines: 1,
                      minFontSize: 10,
                      style: const TextStyle(
                        color: AppColors.inkMuted,
                        fontSize: 13,
                        fontFeatures: [FontFeature.tabularFigures()],
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Flexible(
                    child: AutoResizeText(
                      'Posted ${AppFormatters.date(load.createdAt)}',
                      maxLines: 1,
                      minFontSize: 10,
                      textAlign: TextAlign.end,
                      style: const TextStyle(
                        color: AppColors.inkMuted,
                        fontSize: 13,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _KeyValueRow extends StatelessWidget {
  const _KeyValueRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: const TextStyle(color: AppColors.inkMuted, fontSize: 13),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: AutoResizeText(
            value,
            textAlign: TextAlign.end,
            maxLines: 1,
            minFontSize: 10,
            style: const TextStyle(
              color: AppColors.ink,
              fontSize: 13,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }
}
