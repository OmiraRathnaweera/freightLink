import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/theme/app_colors.dart';

/// A labeled text field matching the mockups' form style: an uppercase-ish
/// small label above a bordered input, with an optional leading icon and
/// inline error text (e.g. "Required field").
class AppTextField extends StatelessWidget {
  const AppTextField({
    super.key,
    required this.label,
    this.controller,
    this.hintText,
    this.prefixIcon,
    this.errorText,
    this.keyboardType,
    this.maxLines = 1,
    this.readOnly = false,
    this.onTap,
    this.suffixIcon,
    this.textInputAction,
    this.obscureText = false,
    this.onChanged,
    this.inputFormatters,
    this.maxLength,
  });

  final String label;
  final TextEditingController? controller;
  final String? hintText;
  final IconData? prefixIcon;
  final String? errorText;
  final TextInputType? keyboardType;
  final int maxLines;
  final bool readOnly;
  final VoidCallback? onTap;
  final Widget? suffixIcon;
  final TextInputAction? textInputAction;
  final bool obscureText;
  final ValueChanged<String>? onChanged;

  /// Filters keystrokes as they're typed (e.g. digits-only for a numeric
  /// field) — a real-time complement to the field's validation *message*,
  /// not a replacement for it.
  final List<TextInputFormatter>? inputFormatters;

  /// Hard cap on input length. The counter is hidden (matches the mockups'
  /// look) — this only enforces the limit, it doesn't display "123/1000".
  final int? maxLength;

  @override
  Widget build(BuildContext context) {
    final hasError = errorText != null && errorText!.isNotEmpty;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            letterSpacing: 0.3,
            color: hasError ? AppColors.statusErrorFg : AppColors.inkMuted,
          ),
        ),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          keyboardType: keyboardType,
          maxLines: maxLines,
          readOnly: readOnly,
          onTap: onTap,
          onChanged: onChanged,
          obscureText: obscureText,
          textInputAction: textInputAction,
          inputFormatters: inputFormatters,
          maxLength: maxLength,
          style: const TextStyle(fontSize: 15, color: AppColors.ink),
          decoration: InputDecoration(
            hintText: hintText,
            prefixIcon: prefixIcon == null
                ? null
                : Icon(prefixIcon, size: 20, color: AppColors.inkMuted),
            suffixIcon: suffixIcon,
            counterText: maxLength == null ? null : '',
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 4),
          Text(
            errorText!,
            style: const TextStyle(
              fontSize: 12,
              color: AppColors.statusErrorFg,
            ),
          ),
        ],
      ],
    );
  }
}
