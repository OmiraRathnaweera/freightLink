import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

/// Categories of in-app status notifications across the 3 mobile roles.
enum NotificationCategory {
  /// Shipper: load matched and approved by dispatch
  loadMatched,

  /// Driver: new trip assigned to vehicle
  tripAssigned,

  /// Agency: job proposal declined, retried, or updated
  proposalUpdate,

  /// General operational notification
  general,
}

/// In-memory model representing a local in-app push notification.
class InAppNotification {
  InAppNotification({
    required this.id,
    required this.title,
    required this.message,
    required this.category,
    required this.timestamp,
    this.isRead = false,
    this.referenceId,
  });

  final String id;
  final String title;
  final String message;
  final NotificationCategory category;
  final DateTime timestamp;
  bool isRead;
  final String? referenceId;

  IconData get icon => switch (category) {
    NotificationCategory.loadMatched => Icons.check_circle_rounded,
    NotificationCategory.tripAssigned => Icons.local_shipping_rounded,
    NotificationCategory.proposalUpdate => Icons.assignment_late_rounded,
    NotificationCategory.general => Icons.notifications_active_rounded,
  };

  Color get iconColor => switch (category) {
    NotificationCategory.loadMatched => AppColors.statusSuccessFg,
    NotificationCategory.tripAssigned => AppColors.primary,
    NotificationCategory.proposalUpdate => AppColors.statusMatchedFg,
    NotificationCategory.general => AppColors.ink,
  };

  Color get iconBackgroundColor => switch (category) {
    NotificationCategory.loadMatched => AppColors.statusSuccessBg,
    NotificationCategory.tripAssigned => AppColors.primary.withValues(alpha: 0.1),
    NotificationCategory.proposalUpdate => AppColors.statusMatchedBg,
    NotificationCategory.general => AppColors.surface,
  };

  InAppNotification copyWith({bool? isRead}) {
    return InAppNotification(
      id: id,
      title: title,
      message: message,
      category: category,
      timestamp: timestamp,
      isRead: isRead ?? this.isRead,
      referenceId: referenceId,
    );
  }
}
