import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/utils/formatters.dart';
import '../models/load.dart';
import '../models/load_status.dart';

/// The vertical status stepper from the "Load Detail — Matched State"
/// mockup ("Draft Created ● Posted to Network ● AI Matched ○ In Transit"),
/// built generically from a load's real `StatusHistory` so it covers every
/// status rather than one bespoke layout per state.
class LoadTimeline extends StatelessWidget {
  const LoadTimeline({
    super.key,
    required this.history,
    required this.currentStatus,
  });

  final List<LoadStatusEvent> history;
  final LoadStatus currentStatus;

  @override
  Widget build(BuildContext context) {
    // Chronological (oldest first) — history arrives newest-first from the API.
    final events = history.reversed.toList();
    final isCancelled = currentStatus == LoadStatus.cancelled;

    return Column(
      children: [
        for (var i = 0; i < events.length; i++)
          _TimelineTile(
            title: LoadStatus.fromWire(events[i].toStatus).label,
            subtitle: events[i].reason,
            timestamp: AppFormatters.time(events[i].changedAt),
            isCompleted: true,
            isLast: i == events.length - 1 && !_hasFuture,
            isError: isCancelled && i == events.length - 1,
          ),
        if (_hasFuture)
          _TimelineTile(
            title: _nextStatus!.label,
            subtitle: 'Pending',
            timestamp: null,
            isCompleted: false,
            isLast: true,
            isError: false,
          ),
      ],
    );
  }

  /// The next status in the normal happy-path progression, shown as a
  /// pending step — omitted once a load reaches a terminal status.
  LoadStatus? get _nextStatus {
    const happyPath = [
      LoadStatus.draft,
      LoadStatus.posted,
      LoadStatus.matched,
      LoadStatus.inTransit,
      LoadStatus.delivered,
    ];
    final index = happyPath.indexOf(currentStatus);
    if (index == -1 || index == happyPath.length - 1) return null;
    return happyPath[index + 1];
  }

  bool get _hasFuture => _nextStatus != null;
}

class _TimelineTile extends StatelessWidget {
  const _TimelineTile({
    required this.title,
    required this.subtitle,
    required this.timestamp,
    required this.isCompleted,
    required this.isLast,
    required this.isError,
  });

  final String title;
  final String? subtitle;
  final String? timestamp;
  final bool isCompleted;
  final bool isLast;
  final bool isError;

  @override
  Widget build(BuildContext context) {
    final dotColor = isError
        ? AppColors.statusErrorFg
        : isCompleted
        ? AppColors.primary
        : AppColors.border;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                width: 14,
                height: 14,
                margin: const EdgeInsets.only(top: 2),
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: isCompleted ? dotColor : Colors.transparent,
                  border: Border.all(color: dotColor, width: 2),
                ),
              ),
              if (!isLast)
                Expanded(child: Container(width: 2, color: AppColors.border)),
            ],
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          title,
                          style: TextStyle(
                            fontWeight: FontWeight.w700,
                            color: isCompleted
                                ? AppColors.ink
                                : AppColors.inkFaint,
                          ),
                        ),
                        if (subtitle != null)
                          Text(
                            subtitle!,
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.inkMuted,
                            ),
                          ),
                      ],
                    ),
                  ),
                  if (timestamp != null)
                    Text(
                      timestamp!,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.inkMuted,
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
