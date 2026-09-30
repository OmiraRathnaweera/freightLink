import 'package:flutter/services.dart';

/// Real-time keystroke filter for a decimal numeric field (digits and a
/// single decimal point) — the same pattern `load_form_screen.dart` uses for
/// Weight/Volume, shared here for other forms with a numeric field
/// (e.g. Add Vehicle's Capacity/Volume).
final decimalInputFormatters = <TextInputFormatter>[
  FilteringTextInputFormatter.allow(RegExp(r'[0-9.]')),
];

/// Shared field-level validation building blocks, mirroring the backend's
/// DataAnnotations 1:1 so a client-side rejection always matches what the API
/// would also reject (same spirit as `features/loads/models/load_validation_rules.dart`,
/// but for the primitives — email/phone/password — repeated across several
/// features' forms instead of one feature's own numeric/length rules).
class AppValidators {
  AppValidators._();

  /// Validates a required numeric field within an inclusive [min]/[max]
  /// range — mirrors a `[Required]` + `[Range(min, max)]` pair.
  static String? number(String value, String fieldLabel, {required double min, required double max}) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return '$fieldLabel is required.';
    final parsed = double.tryParse(trimmed);
    if (parsed == null) return 'Enter a valid number.';
    if (parsed < min || parsed > max) {
      return '$fieldLabel must be between $min and $max.';
    }
    return null;
  }

  /// Matches the backend's `AuthPatterns.EmailPattern` exactly
  /// (`backend/Common/Validation/AuthPatterns.cs`), which itself mirrors the
  /// `ck_user_email_format` Postgres CHECK constraint.
  static final RegExp emailPattern = RegExp(r'^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$');

  /// Matches the backend's `AuthPatterns.PhonePattern` exactly — E.164,
  /// mandatory leading `+`.
  static final RegExp phonePattern = RegExp(r'^\+[1-9]\d{1,14}$');

  /// Matches the backend's `StrongPasswordAttribute` pattern exactly: at
  /// least 8 characters, with an uppercase letter, a lowercase letter, a
  /// digit, and a special character.
  static final RegExp strongPasswordPattern = RegExp(r'^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$');

  /// BCrypt's hard input limit in UTF-8 bytes (`PasswordPolicy.MaxBytes`) —
  /// anything beyond this is silently truncated by the hasher, so a longer
  /// password would misleadingly seem to "work" at a different length than
  /// what was actually typed.
  static const int passwordMaxBytes = 72;

  /// Required-field check with a field-name-aware message, e.g.
  /// `requiredError('Email')` -> `'Email is required.'`.
  static String? required(String value, String fieldLabel) {
    return value.trim().isEmpty ? '$fieldLabel is required.' : null;
  }

  /// `[Required]` + `[EmailAddress]` + `[RegularExpression(EmailPattern)]` +
  /// `[StringLength(256)]`, mirrored in that order.
  static String? email(String value, {int maxLength = 256}) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return 'Email is required.';
    if (trimmed.length > maxLength) return 'Email must be $maxLength characters or fewer.';
    if (!emailPattern.hasMatch(trimmed)) return 'Enter a valid email address.';
    return null;
  }

  /// `[RegularExpression(PhonePattern)]` — optional field, so an empty value
  /// is valid; only a non-empty value is checked against the E.164 pattern.
  static String? optionalPhone(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return null;
    if (!phonePattern.hasMatch(trimmed)) {
      return 'Enter a valid phone number in E.164 format (e.g. +14155552671).';
    }
    return null;
  }

  /// `[Required]` + `[StrongPassword]` + the 72-UTF8-byte BCrypt cap.
  static String? strongPassword(String value) {
    if (value.isEmpty) return 'Password is required.';
    if (_utf8ByteLength(value) > passwordMaxBytes) {
      return 'Password must be $passwordMaxBytes bytes or fewer.';
    }
    if (!strongPasswordPattern.hasMatch(value)) {
      return 'Must be at least 8 characters with an uppercase letter, a lowercase letter, a digit, and a special character.';
    }
    return null;
  }

  /// `[Required]` only (login doesn't re-check strength, just presence, plus
  /// the same 72-byte cap so an over-long password isn't silently truncated
  /// by BCrypt without the user knowing why sign-in "fails").
  static String? password(String value) {
    if (value.isEmpty) return 'Password is required.';
    if (_utf8ByteLength(value) > passwordMaxBytes) {
      return 'Password must be $passwordMaxBytes bytes or fewer.';
    }
    return null;
  }

  /// `[Required]` + `[StringLength(max, MinimumLength = min)]`.
  static String? length(
    String value,
    String fieldLabel, {
    required int max,
    int min = 0,
    bool isRequired = true,
  }) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      return isRequired ? '$fieldLabel is required.' : null;
    }
    if (trimmed.length < min) {
      return '$fieldLabel must be at least $min characters.';
    }
    if (trimmed.length > max) {
      return '$fieldLabel must be $max characters or fewer.';
    }
    return null;
  }

  static int _utf8ByteLength(String value) {
    var length = 0;
    for (final rune in value.runes) {
      if (rune <= 0x7F) {
        length += 1;
      } else if (rune <= 0x7FF) {
        length += 2;
      } else if (rune <= 0xFFFF) {
        length += 3;
      } else {
        length += 4;
      }
    }
    return length;
  }
}
