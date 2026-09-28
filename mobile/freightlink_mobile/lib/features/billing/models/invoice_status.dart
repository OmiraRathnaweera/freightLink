import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';

/// Mirrors the backend's `InvoiceStatus` enum
/// (`backend/Entities/Enums/InvoiceStatus.cs`) exactly — these are the only
/// statuses the API can ever return. `Dart` reserves `void` as a keyword, so
/// that member is named `void_`, consistent with how this codebase names
/// other reserved-word-colliding enum members.
enum InvoiceStatus {
  draft,
  issued,
  paymentPending,
  paid,
  failed,
  void_;

  /// Parses the API's PascalCase wire value (e.g. `"PaymentPending"`).
  static InvoiceStatus fromWire(String value) {
    return InvoiceStatus.values.firstWhere(
      (status) => status.wireName.toLowerCase() == value.toLowerCase(),
      orElse: () => throw ArgumentError('Unknown InvoiceStatus: $value'),
    );
  }

  String get wireName {
    switch (this) {
      case InvoiceStatus.draft:
        return 'Draft';
      case InvoiceStatus.issued:
        return 'Issued';
      case InvoiceStatus.paymentPending:
        return 'PaymentPending';
      case InvoiceStatus.paid:
        return 'Paid';
      case InvoiceStatus.failed:
        return 'Failed';
      case InvoiceStatus.void_:
        return 'Void';
    }
  }

  /// Display label for badges, matching the web app's wording.
  String get label {
    switch (this) {
      case InvoiceStatus.draft:
        return 'Draft';
      case InvoiceStatus.issued:
        return 'Issued';
      case InvoiceStatus.paymentPending:
        return 'Payment Pending';
      case InvoiceStatus.paid:
        return 'Paid';
      case InvoiceStatus.failed:
        return 'Failed';
      case InvoiceStatus.void_:
        return 'Void';
    }
  }

  Color get foreground {
    switch (this) {
      case InvoiceStatus.paid:
        return AppColors.statusSuccessFg;
      case InvoiceStatus.issued:
        return AppColors.statusInTransitFg;
      case InvoiceStatus.paymentPending:
        return AppColors.statusMatchedFg;
      case InvoiceStatus.draft:
        return AppColors.statusNeutralFg;
      case InvoiceStatus.failed:
      case InvoiceStatus.void_:
        return AppColors.statusErrorFg;
    }
  }

  Color get background {
    switch (this) {
      case InvoiceStatus.paid:
        return AppColors.statusSuccessBg;
      case InvoiceStatus.issued:
        return AppColors.statusInTransitBg;
      case InvoiceStatus.paymentPending:
        return AppColors.statusMatchedBg;
      case InvoiceStatus.draft:
        return AppColors.statusNeutralBg;
      case InvoiceStatus.failed:
      case InvoiceStatus.void_:
        return AppColors.statusErrorBg;
    }
  }

  /// Mirrors `InvoiceStatusTransitionRules`: Draft/Issued only (Agent only).
  bool get isEditable => this == InvoiceStatus.draft;

  /// Draft -> Issued (Agent only).
  bool get canIssue => this == InvoiceStatus.draft;

  /// Draft/Issued/PaymentPending/Failed -> Void (Agent only).
  bool get isVoidable =>
      this == InvoiceStatus.draft ||
      this == InvoiceStatus.issued ||
      this == InvoiceStatus.paymentPending ||
      this == InvoiceStatus.failed;

  /// Issued/PaymentPending/Failed -> PaymentPending (Shipper submits/resubmits proof).
  bool get canSubmitPaymentProof =>
      this == InvoiceStatus.issued || this == InvoiceStatus.paymentPending || this == InvoiceStatus.failed;
}
