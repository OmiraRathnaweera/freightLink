import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

/// Mirrors the backend's `LoadStatus` enum (`backend/Entities/Enums/LoadStatus.cs`)
/// exactly — these are the only statuses the API can ever return.
enum LoadStatus {
  draft,
  posted,
  matched,
  inTransit,
  delivered,
  closed,
  cancelled;

  /// Parses the API's PascalCase wire value (e.g. `"InTransit"`).
  static LoadStatus fromWire(String value) {
    return LoadStatus.values.firstWhere(
      (status) => status.wireName.toLowerCase() == value.toLowerCase(),
      orElse: () => throw ArgumentError('Unknown LoadStatus: $value'),
    );
  }

  String get wireName {
    switch (this) {
      case LoadStatus.draft:
        return 'Draft';
      case LoadStatus.posted:
        return 'Posted';
      case LoadStatus.matched:
        return 'Matched';
      case LoadStatus.inTransit:
        return 'InTransit';
      case LoadStatus.delivered:
        return 'Delivered';
      case LoadStatus.closed:
        return 'Closed';
      case LoadStatus.cancelled:
        return 'Cancelled';
    }
  }

  /// Display label for badges/timelines, matching the mockups' wording.
  String get label {
    switch (this) {
      case LoadStatus.draft:
        return 'Draft';
      case LoadStatus.posted:
        return 'Posted';
      case LoadStatus.matched:
        return 'Matched';
      case LoadStatus.inTransit:
        return 'In Transit';
      case LoadStatus.delivered:
        return 'Delivered';
      case LoadStatus.closed:
        return 'Closed';
      case LoadStatus.cancelled:
        return 'Cancelled';
    }
  }

  Color get foreground {
    switch (this) {
      case LoadStatus.matched:
        return AppColors.statusMatchedFg;
      case LoadStatus.inTransit:
        return AppColors.statusInTransitFg;
      case LoadStatus.delivered:
      case LoadStatus.closed:
        return AppColors.statusSuccessFg;
      case LoadStatus.cancelled:
        return AppColors.statusErrorFg;
      case LoadStatus.draft:
      case LoadStatus.posted:
        return AppColors.statusNeutralFg;
    }
  }

  Color get background {
    switch (this) {
      case LoadStatus.matched:
        return AppColors.statusMatchedBg;
      case LoadStatus.inTransit:
        return AppColors.statusInTransitBg;
      case LoadStatus.delivered:
      case LoadStatus.closed:
        return AppColors.statusSuccessBg;
      case LoadStatus.cancelled:
        return AppColors.statusErrorBg;
      case LoadStatus.draft:
      case LoadStatus.posted:
        return AppColors.statusNeutralBg;
    }
  }

  /// Whether `PUT /loads/{id}` (edit) is allowed for a load in this status —
  /// mirrors the backend's edit rule (Draft/Posted only).
  bool get isEditable => this == LoadStatus.draft || this == LoadStatus.posted;

  /// Whether `PATCH /loads/{id}/status` → Cancelled is allowed from this
  /// status, mirroring `LoadStatusTransitionRules.CanCancel`.
  bool get isCancellable =>
      this == LoadStatus.draft ||
      this == LoadStatus.posted ||
      this == LoadStatus.matched;

  /// Whether this load can still be published (Draft → Posted).
  bool get canPost => this == LoadStatus.draft;
}
