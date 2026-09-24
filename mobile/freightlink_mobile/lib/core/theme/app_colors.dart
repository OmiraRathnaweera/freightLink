import 'package:flutter/material.dart';

/// Color tokens for the FreightLink design system, sourced from the Loads
/// feature mockups (`~/Downloads/flutter uis`). Light-only for now — the
/// mockups have no dark variant.
class AppColors {
  AppColors._();

  // Surfaces.
  static const Color background = Color(0xFFF5F6F8);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color border = Color(0xFFE5E7EB);

  // Text.
  static const Color ink = Color(0xFF0F172A);
  static const Color inkMuted = Color(0xFF6B7280);
  static const Color inkFaint = Color(0xFF9CA3AF);

  // Primary action (buttons, active nav item, FAB).
  static const Color primary = Color(0xFF0F172A);
  static const Color onPrimary = Color(0xFFFFFFFF);

  // Status: Matched.
  static const Color statusMatchedFg = Color(0xFF2563EB);
  static const Color statusMatchedBg = Color(0xFFDBEAFE);

  // Status: In Transit.
  static const Color statusInTransitFg = Color(0xFFB45309);
  static const Color statusInTransitBg = Color(0xFFFEF3C7);

  // Status: Delivered / success.
  static const Color statusSuccessFg = Color(0xFF15803D);
  static const Color statusSuccessBg = Color(0xFFDCFCE7);

  // Status: Draft / Closed / neutral.
  static const Color statusNeutralFg = Color(0xFF4B5563);
  static const Color statusNeutralBg = Color(0xFFF3F4F6);

  // Status: Cancelled / error.
  static const Color statusErrorFg = Color(0xFFDC2626);
  static const Color statusErrorBg = Color(0xFFFEE2E2);
}
