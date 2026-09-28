import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../shared/widgets/status_pill.dart';

/// Trip status → color mapping, centralized here instead of the inline
/// per-screen ternaries elsewhere in this feature (`driver_assigned_trip_screen.dart`,
/// `accept_load_assign_screen.dart`, etc.) — mirrors the web app's
/// `getTripStatusTone` (`frontend/src/features/trips/lib/statusTone.js`)
/// exactly, so a status reads the same color on both platforms:
/// Assigned=neutral, PickedUp=blue, InTransit=amber, Delivered=green, Cancelled=red.
class TripStatusStyle {
  TripStatusStyle._();

  static (Color foreground, Color background) colorsFor(String status) {
    switch (status) {
      case 'PickedUp':
      case 'picked_up':
        return (AppColors.statusMatchedFg, AppColors.statusMatchedBg);
      case 'InTransit':
      case 'in_transit':
        return (AppColors.statusInTransitFg, AppColors.statusInTransitBg);
      case 'Delivered':
      case 'delivered':
        return (AppColors.statusSuccessFg, AppColors.statusSuccessBg);
      case 'Cancelled':
      case 'cancelled':
        return (AppColors.statusErrorFg, AppColors.statusErrorBg);
      case 'Assigned':
      case 'assigned':
      default:
        return (AppColors.statusNeutralFg, AppColors.statusNeutralBg);
    }
  }
}

/// A [StatusPill] pre-wired with the right colors for a trip status string.
class TripStatusPill extends StatelessWidget {
  const TripStatusPill({super.key, required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final (fg, bg) = TripStatusStyle.colorsFor(status);
    return StatusPill(label: status, foreground: fg, background: bg);
  }
}
