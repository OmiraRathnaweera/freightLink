import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:freightlink_mobile/shared/widgets/auto_resize_text.dart';

void main() {
  group('AutoResizeText Widget Tests', () {
    testWidgets('renders text inside normal constraints', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: SizedBox(
              width: 300,
              child: AutoResizeText(
                'Short text',
                style: TextStyle(fontSize: 16),
              ),
            ),
          ),
        ),
      );

      expect(find.text('Short text'), findsOneWidget);
    });

    testWidgets('does not overflow when long text is inside a narrow container', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: SizedBox(
              width: 100,
              child: AutoResizeText(
                'A very long text string that would normally overflow a 100px box',
                maxLines: 1,
                minFontSize: 8,
                style: TextStyle(fontSize: 20),
              ),
            ),
          ),
        ),
      );

      // Verify widget rendered cleanly with no flutter overflow error
      expect(find.byType(AutoResizeText), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('AutoResizeText.kpi renders metric text without overflow', (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: SizedBox(
              width: 120,
              child: AutoResizeText.kpi(
                'LKR 1,250,000.00',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
              ),
            ),
          ),
        ),
      );

      expect(find.byType(AutoResizeText), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('responsiveFont scales proportionally based on screen width', (tester) async {
      late double scaledFont;

      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) {
              scaledFont = context.responsiveFont(16.0);
              return const SizedBox();
            },
          ),
        ),
      );

      // Default tester width is 800 (maxScale 1.25 applies: 16 * 1.25 = 20)
      expect(scaledFont, greaterThanOrEqualTo(16.0));
      expect(scaledFont, lessThanOrEqualTo(20.0));
    });
  });
}
