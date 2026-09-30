import 'package:auto_size_text/auto_size_text.dart';
import 'package:flutter/material.dart';

/// Extension on [BuildContext] for responsive typography scaling based on screen width.
extension ResponsiveTypographyX on BuildContext {
  /// Proportional font size based on standard mobile width (390dp).
  /// Clamped between [minScale] (0.85) and [maxScale] (1.25) to maintain legibility.
  double responsiveFont(
    double baseFontSize, {
    double minScale = 0.85,
    double maxScale = 1.25,
  }) {
    final screenWidth = MediaQuery.sizeOf(this).width;
    final scale = (screenWidth / 390.0).clamp(minScale, maxScale);
    return (baseFontSize * scale).roundToDouble();
  }
}

/// A responsive, auto-resizing text widget that dynamically scales font size
/// to fit its parent container's width/height constraints without overflowing.
///
/// Combines screen-width responsive scaling with [AutoSizeText] dynamic font
/// stepping and graceful ellipsis fallback.
class AutoResizeText extends StatelessWidget {
  const AutoResizeText(
    this.text, {
    super.key,
    this.style,
    this.minFontSize = 9.0,
    this.maxFontSize = double.infinity,
    this.maxLines = 1,
    this.overflow = TextOverflow.ellipsis,
    this.textAlign,
    this.stepGranularity = 0.5,
    this.responsiveScale = false,
  });

  /// Convenience constructor for single-line KPI metrics and large numbers.
  const AutoResizeText.kpi(
    this.text, {
    super.key,
    this.style,
    this.minFontSize = 12.0,
    this.maxFontSize = double.infinity,
    this.maxLines = 1,
    this.overflow = TextOverflow.ellipsis,
    this.textAlign,
    this.stepGranularity = 0.5,
    this.responsiveScale = false,
  });

  final String text;
  final TextStyle? style;
  final double minFontSize;
  final double maxFontSize;
  final int? maxLines;
  final TextOverflow overflow;
  final TextAlign? textAlign;
  final double stepGranularity;
  final bool responsiveScale;

  @override
  Widget build(BuildContext context) {
    var effectiveStyle = style ?? DefaultTextStyle.of(context).style;

    if (responsiveScale && effectiveStyle.fontSize != null) {
      final scaledFontSize = context.responsiveFont(effectiveStyle.fontSize!);
      effectiveStyle = effectiveStyle.copyWith(fontSize: scaledFontSize);
    }

    return AutoSizeText(
      text,
      style: effectiveStyle,
      minFontSize: minFontSize,
      maxFontSize: maxFontSize,
      maxLines: maxLines,
      overflow: overflow,
      textAlign: textAlign,
      stepGranularity: stepGranularity,
    );
  }
}
