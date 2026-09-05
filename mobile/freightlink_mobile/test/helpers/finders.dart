import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/shared/widgets/app_text_field.dart';

/// Enters [text] into the `TextField` inside the [AppTextField] labeled
/// [label] — `AppTextField` itself isn't editable directly, only the
/// `TextField` it wraps.
Future<void> enterAppTextField(
  WidgetTester tester,
  String label,
  String text,
) async {
  final field = find.descendant(
    of: find.widgetWithText(AppTextField, label),
    matching: find.byType(TextField),
  );
  await tester.enterText(field, text);
}
